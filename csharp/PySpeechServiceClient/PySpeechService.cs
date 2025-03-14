using System.Collections.Concurrent;
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PySpeechServiceClient.Grammar;
using PySpeechServiceClient.Models;
using Microsoft.Extensions.DependencyInjection;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PySpeechServiceClient;

internal class PySpeechService(PySpeechServiceRunner runner, IServiceProvider serviceProvider)
    : IPySpeechService
{
    private readonly ILogger<PySpeechService>? _logger = serviceProvider.GetService<ILogger<PySpeechService>>();
    private readonly Dictionary<string, SpeechRecognitionGrammar> _commands = [];
    private readonly SemaphoreSlim _requestSemaphore = new(0);
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource> _taskCompletionSources = [];
    private readonly ConcurrentQueue<SpeechServiceRequest> _requests = new();
    private readonly ConcurrentQueue<ulong> _pendingMessageIds = [];
    private GrpcChannel? _channel;
    private CancellationTokenSource? _cts;
    private AsyncDuplexStreamingCall<SpeechServiceRequest, SpeechServiceResponse>? _speechGrpcService;
    private Task? _receiveTask;
    private Task? _sendTask;
    private bool _isSpeechSetup;
    private bool _isSpeechRecognitionSetup;
    private int _port;
    private DateTime? _lastResponseTime;
    private bool _hasSentStoppedEvent;
    private bool _hasPerformedFirstInit;
    private IDictionary<string, string>? _replacements;
    private ulong _currentId;

    public bool IsConnected { get; private set; }
    
    public bool AutoReconnect { get; set; }
    
    public bool IsSpeechEnabled => IsConnected && _speechGrpcService is not null && _isSpeechSetup;
    
    public bool IsSpeechRecognitionEnabled => IsConnected && _speechGrpcService is not null && _isSpeechRecognitionSetup;
    
    public event EventHandler? Initialized;
    public event EventHandler? Disconnected;
    public event EventHandler? TextToSpeechInitialized;
    public event EventHandler? SpeechRecognitionInitialized;
    public event EventHandler<SpeakCommandResponseEventArgs>? SpeakCommandResponded;
    public event EventHandler<SpeechRecognitionResultEventArgs>? SpeechRecognized;

    public async Task<bool> StartAsync()
    {
        if (!_hasPerformedFirstInit)
        {
            _hasPerformedFirstInit = true;
            runner.ProcessEnded += async(_, _) =>
            {
                await CleanupAsync();
            };
        }
        
        _hasSentStoppedEvent = false;
        
        // Attempt to start the service 3 times
        for (var i = 0; i < 3; i++)
        {
            _logger?.LogInformation("PySpeechService Initialization Attempt {Number}", i + 1);

            try
            {
                var response = await InitAttempt();
                if (response)
                {
                    _logger?.LogInformation("PySpeechService initialization successful");
                    Initialized?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _logger?.LogError("PySpeechService initialization failed");
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "PySpeechService initialization failed");
            }

            if (i < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        return false;
    }

    public void AddSpeechRecognitionCommand(SpeechRecognitionGrammar command)
    {
        if (string.IsNullOrEmpty(command.RuleName) || _commands.ContainsKey(command.RuleName))
        {
            command.RuleName = (command.RuleName ?? "") + Guid.NewGuid();
        }

        _commands[command.RuleName] = command;
    }

    public void AddSpeechRecognitionReplacements(IDictionary<string, string> replacements)
    {
        _replacements = replacements;
    }

    public void Speak(string message, Models.SpeechSettings? details = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        
        var source = new TaskCompletionSource();
        _currentId++;
        
        var id = _currentId;
        _taskCompletionSources.TryAdd(id, source);
        _pendingMessageIds.Enqueue(id);
        
        _ = SendSpeakRequest(message, details, id);
        
        source.Task.GetAwaiter().GetResult();
        
        _taskCompletionSources.TryRemove(id, out _);
        while (_pendingMessageIds.TryPeek(out var otherId) && otherId <= id)
        {
            if (_pendingMessageIds.TryDequeue(out otherId))
            {
                if (otherId != id && _taskCompletionSources.TryGetValue(otherId, out var otherTcs))
                {
                    try
                    {
                        otherTcs.TrySetResult();
                    }
                    catch (Exception)
                    {
                        // do nothing
                    }
                }
            }
        }
    }
    
    public Task<bool> SpeakAsync(string message, Models.SpeechSettings? details = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return Task.FromResult(false);
        }
        
        return SendSpeakRequest(message, details);
    }

    public Task<bool> StopSpeakingAsync()
    {
        return Task.FromResult(SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            StopSpeaking = new StopSpeakingRequest()
        }));
    }

    public Task<bool> SetSpeechSettingsAsync(Models.SpeechSettings settings)
    {
        var speechSettings = new SetSpeechSettingsRequest()
        {
            SpeechSettings = settings.ToSpeechSettings(),
        };

        return Task.FromResult(SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            SetSpeechSettings = speechSettings
        }));
    }

    public async Task<bool> StartSpeechRecognitionAsync(string? voskModel = null, double requiredConfidence = 80, string prefix = "")
    {
        var rules = _commands.Values.Select(x => x.RuleGrammarElement).ToList();
        var toJsonObject = new Dictionary<string, object>()
        {
            { "Rules", rules },
            { "Replacements", _replacements ?? new Dictionary<string, string>() },
            { "Prefix", prefix }
        };
        var tempFile = Path.GetTempPath() + Guid.NewGuid() + ".json";
        var json = JsonConvert.SerializeObject(toJsonObject);
        
        try
        {
            await File.WriteAllTextAsync(tempFile, json);
            _logger?.LogInformation("Writing PySpeechService grammar file to {Path}", tempFile);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unable to write PySpeechService grammar file");
            return false;
        }
        
        _logger?.LogInformation("Attempting to start speech recognition");
        return SendSpeechServiceRequest(new SpeechServiceRequest
        {
            StartSpeechRecognition = new StartSpeechRecognitionRequest
            {
                VoskModel = voskModel ?? "",
                GrammarFile = tempFile,
                RequiredConfidence = requiredConfidence
            }
        });
    }

    public Task<bool> StopSpeechRecognitionAsync()
    {
        _commands.Clear(); 
        return Task.FromResult(SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            StopSpeechRecognition = new StopSpeechRecognitionRequest()
        }));
    }

    public async Task<bool> ShutdownAsync()
    {
        SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            Shutdown = new ShutdownRequest()
        });

        await CleanupAsync();

        return true;
    }

    public Task<bool> SetVolumeAsync(double volume)
    {
        var result = SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            SetVolume = new SetSpeechVolumeRequest()
            {
                Volume = volume
            }
        });

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        _ = CleanupAsync();
        _speechGrpcService?.Dispose();
        _channel?.Dispose();
        runner.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<bool> SendSpeakRequest(string message, Models.SpeechSettings? details = null, ulong? id = null)
    {
        if (!_isSpeechSetup)
        {
            _logger?.LogWarning("PySpeechService Speech Settings are not setup. Please call SetSpeechSettingsAsync.");
            return Task.FromResult(false);
        }

        id ??= _currentId + 1;
        _currentId = id.Value;
        
        return Task.FromResult(SendSpeechServiceRequest(new SpeechServiceRequest()
        {
            Speak = new SpeakRequest()
            {
                Message = message,
                SpeechSettings = details?.ToSpeechSettings(),
                MessageId = _currentId
            }
        }));
    }
    
    private async Task SendRequests()
    {
        if (_speechGrpcService == null)
        {
            return;
        }

        try
        {
            var cts = _cts!;

            while (cts.Token.IsCancellationRequested == false)
            {
                await _requestSemaphore.WaitAsync(cts.Token);
                while (cts.Token.IsCancellationRequested == false && _requests.TryDequeue(out var request))
                {
                    try
                    {
                        await _speechGrpcService.RequestStream.WriteAsync(request, cts.Token);
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, "Failed to make PySpeechService call {Call}",
                            JsonSerializer.Serialize(request));
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to write gRPC service");
        }
            
    }
    
    private async Task ReceiveResponses()
    {
        if (_speechGrpcService == null)
        {
            return;
        }
        
        try
        {
            await foreach (var response in _speechGrpcService.ResponseStream.ReadAllAsync(_cts!.Token))
            {
                _lastResponseTime = DateTime.Now;
                
                if (response.SpeakUpdate != null)
                {
                    if (response.SpeakUpdate.IsEndOfMessage &&
                        _taskCompletionSources.TryGetValue(response.SpeakUpdate.MessageId, out var tcs))
                    {
                        tcs.TrySetResult();
                    }
                    else if (_pendingMessageIds.TryPeek(out var pendingMessageId) && response.SpeakUpdate.MessageId > pendingMessageId)
                    {
                        for (var i = pendingMessageId; i < response.SpeakUpdate.MessageId; i++)
                        {
                            if (_taskCompletionSources.TryGetValue(i, out var iTcs))
                            {
                                try
                                {
                                    iTcs.TrySetResult();
                                }
                                catch (Exception)
                                {
                                    // do nothing
                                }
                            }
                        }
                    }
                    
                    SpeakCommandResponded?.Invoke(this, new SpeakCommandResponseEventArgs(new SpeakCommandResponse()
                    {
                        FullMessage = response.SpeakUpdate.Message,
                        CurrentChunk = response.SpeakUpdate.Chunk,
                        IsStartOfMessage = response.SpeakUpdate.IsStartOfMessage,
                        IsStartOfChunk = response.SpeakUpdate.IsStartOfChunk,
                        IsEndOfMessage = response.SpeakUpdate.IsEndOfMessage,
                        IsEndOfChunk = response.SpeakUpdate.IsEndOfChunk,
                        HasAnotherRequest = response.SpeakUpdate.HasAnotherRequest
                    }));
                }
                else if (response.SpeechRecognized != null)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            SpeechRecognized?.Invoke(this, new SpeechRecognitionResultEventArgs(new SpeechRecognitionResult()
                            {
                                Text = response.SpeechRecognized.RecognizedText,
                                Confidence = (float)response.SpeechRecognized.Confidence / 100.0f,
                                Semantics = response.SpeechRecognized.Semantics.ToDictionary(x => x.Key, x => new SpeechRecognitionSemantic(x.Key, x.Value))
                            }));
                    
                            if (!_commands.TryGetValue(response.SpeechRecognized.RecognizedRule, out var command))
                            {
                                return;
                            }
                        
                            command.OnSpeechRecognized(response.SpeechRecognized.RecognizedText,
                                (float)response.SpeechRecognized.Confidence / 100.0f,
                                response.SpeechRecognized.Semantics.ToDictionary(x => x.Key,
                                    x => new SpeechRecognitionSemantic(x.Key, x.Value)));
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, "Error handling recognized speech");
                        }
                        
                    });

                }
                else if (response.SpeechRecognitionStarted != null)
                {
                    _isSpeechRecognitionSetup = response.SpeechRecognitionStarted.Successful;
                    
                    if (response.SpeechRecognitionStarted.Successful)
                    {
                        _logger?.LogInformation("Speech recognition initialization successful");
                        _logger?.LogWarning("Speech recognition ignored words: {Words}", string.Join(',', runner.IgnoredWords));
                        SpeechRecognitionInitialized?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        _logger?.LogError("Speech recognition initialization failed");
                    }
                }
                else if (response.SpeechSettingsSet != null)
                {
                    _isSpeechSetup = response.SpeechSettingsSet.Successful;
                    
                    if (response.SpeechSettingsSet.Successful)
                    {
                        _logger?.LogInformation("Text to speech initialization successful");
                        TextToSpeechInitialized?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        _logger?.LogError("Text to speech initialization successful");
                    }
                }
                else if (response.Error != null)
                {
                    _logger?.LogError("Error received from PySpeechService: {Error}", response.Error);
                }
                else if (response.SetVolume != null)
                {
                    _logger?.LogInformation("Volume set {Value}", response.SetVolume.Successful ? "successfully" : "failed");
                }
                else if (response.Ping == null)
                {
                    _logger?.LogWarning("Unknown response from PySpeechService: " + JsonSerializer.Serialize(response));
                }
            }
        }
        catch (RpcException ex)
        {
            _logger?.LogWarning(ex, "Connection to PySpeechService closed");
            _ = CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        var previouslyRunning = IsConnected;
        
        runner.EndProcess();
        
        if (!_hasSentStoppedEvent)
        {
            _hasSentStoppedEvent = true;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        
        _isSpeechRecognitionSetup = false;
        _isSpeechSetup = false;
        IsConnected = false;
                
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }
        
        _requestSemaphore.Release();
        
        if (_speechGrpcService != null)
        {
            await _speechGrpcService.RequestStream.CompleteAsync();
        }
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }
        
        if (_sendTask != null)
        {
            await _sendTask;
        }

        if (previouslyRunning && AutoReconnect)
        {
            _ = StartAsync();
        }
    }

    private async Task Monitor(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _speechGrpcService != null)
        {
            var diff = DateTime.Now - _lastResponseTime;
            if (diff?.TotalSeconds > 90 || !runner.IsRunning)
            {
                await CleanupAsync();
            }
            else if (diff == null || diff.Value.TotalSeconds > 25)
            {
                SendSpeechServiceRequest(new SpeechServiceRequest()
                {
                    Ping = new PingRequest
                    {
                        Time = DateTime.Now.ToLongTimeString()
                    }
                });
            }
            
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }
    
    private async Task<bool> InitAttempt()
    {
        // Cleanup the previous attempt if needed
        await CleanupAsync();
        
        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;
        _lastResponseTime = null;
        
        if (!await runner.StartAsync())
        {
            _logger?.LogError("Could not start PySpeechService process");
            runner.EndProcess();
            return false;
        }
        
        _port = runner.Port;
        _channel = GrpcChannel.ForAddress($"http://localhost:{_port}");
        var client = new SpeechService.SpeechServiceClient(_channel);
        _speechGrpcService = client.StartSpeechService(cancellationToken: cancellationToken);
        _receiveTask = Task.Run(ReceiveResponses, cancellationToken);
        _sendTask = Task.Run(SendRequests, cancellationToken);

        // Make 3 attempts to send the first ping request and receive a response
        for (var i = 0; i < 3; i++)
        {
            var sendSuccessful = SendSpeechServiceRequest(new SpeechServiceRequest()
            {
                Ping = new PingRequest
                {
                    Time = DateTime.Now.ToLongTimeString()
                }
            }, true);

            if (sendSuccessful)
            {
                // Give 10 seconds to receive a response
                for (var j = 0; j < 100; j++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.1), cancellationToken);
                    if (_lastResponseTime != null)
                    {
                        IsConnected = true;
                        _ = Monitor(cancellationToken);
                        return true;
                    }
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(0.3), cancellationToken);
        }
        
        _logger?.LogError("Failed to receive ping response from PySpeechService");
        return false;
    }
    
    private bool SendSpeechServiceRequest(SpeechServiceRequest request, bool isInitialRequest = false)
    {
        if (_speechGrpcService == null || (!isInitialRequest && !IsConnected))
        {
            return false;
        }

        try
        {
            var sendUpdate = _requests.IsEmpty;
            _requests.Enqueue(request);
            if (sendUpdate)
            {
                _requestSemaphore.Release();
            }

            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to add request to queue");
            return false;
        }
    }
}