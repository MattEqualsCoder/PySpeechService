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
    private GrpcChannel? _channel;
    private CancellationTokenSource? _cts;
    private AsyncDuplexStreamingCall<SpeechServiceRequest, SpeechServiceResponse>? _speechGrpcService;
    private Task? _receiveTask;
    private bool _isSpeechSetup;
    private bool _isSpeechRecognitionSetup;
    private int _port;
    private DateTime? _lastResponseTime;
    private bool _hasSentStoppedEvent;
    private bool _hasPerformedFirstInit;

    public bool IsConnected { get; private set; }
    
    public bool IsSpeechEnabled => IsConnected && _speechGrpcService is not null && _isSpeechSetup;
    
    public bool IsSpeechRecognitionEnabled => IsConnected && _speechGrpcService is not null && _isSpeechRecognitionSetup;
    
    public event EventHandler? Started;
    public event EventHandler? Stopped;
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
            _logger?.LogInformation("PySpeechService Initialization Attempt {Number}", i);

            try
            {
                var response = await InitAttempt();
                if (response)
                {
                    _logger?.LogInformation("PySpeechService initialization successful");
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

    public async Task<bool> SpeakAsync(string message, Models.SpeechSettings? details = null)
    {
        if (!_isSpeechSetup)
        {
            _logger?.LogWarning("PySpeechService Speech Settings are not setup. Please call SetSpeechSettingsAsync.");
            return false;
        }
        
        return await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
        {
            Speak = new SpeakRequest()
            {
                Message = message,
                SpeechSettings = details?.ToSpeechSettings()
            }
        });
    }

    public async Task StopSpeakingAsync()
    {
        await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
        {
            StopSpeaking = new StopSpeakingRequest()
        });
    }

    public async Task SetSpeechSettingsAsync(Models.SpeechSettings settings)
    {
        var speechSettings = new SetSpeechSettingsRequest()
        {
            SpeechSettings = settings.ToSpeechSettings(),
        };

        await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
        {
            SetSpeechSettings = speechSettings
        });
    }

    public async Task StartSpeechRecognitionAsync(string? voskModel = null, double requiredConfidence = 80)
    {
        var rules = _commands.Values.Select(x => x.RuleGrammarElement).ToList();
        var tempFile = Path.GetTempPath() + Guid.NewGuid() + ".json";
        var json = JsonConvert.SerializeObject(rules);
        await File.WriteAllTextAsync(tempFile, json);
        await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
        {
            StartSpeechRecognition = new StartSpeechRecognitionRequest()
            {
                VoskModel = voskModel ?? "",
                GrammarFile = tempFile,
                RequiredConfidence = requiredConfidence
            }
        });
    }
    
    public async Task ShutdownAsync()
    {
        await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
        {
            Shutdown = new ShutdownRequest()
        });

        await CleanupAsync();
    }
    
    public void Dispose()
    {
        _ = CleanupAsync();
        _speechGrpcService?.Dispose();
        _channel?.Dispose();
        runner.Dispose();
        GC.SuppressFinalize(this);
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
                    SpeakCommandResponded?.Invoke(this, new SpeakCommandResponseEventArgs(new SpeakCommandResponse()
                    {
                        FullMessage = response.SpeakUpdate.Message,
                        CurrentChunk = response.SpeakUpdate.Chunk,
                        IsStartOfMessage = response.SpeakUpdate.IsStartOfMessage,
                        IsStartOfChunk = response.SpeakUpdate.IsStartOfChunk,
                        IsEndOfMessage = response.SpeakUpdate.IsEndOfMessage,
                        IsEndOfChunk = response.SpeakUpdate.IsEndOfChunk,
                    }));
                }
                else if (response.SpeechRecognized != null)
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
                        response.SpeechRecognized.Semantics.ToDictionary(x => x.Key, x => new SpeechRecognitionSemantic(x.Key, x.Value)));
                }
                else if (response.SpeechRecognitionStarted != null)
                {
                    _isSpeechRecognitionSetup = response.SpeechRecognitionStarted.Successful;
                    
                    if (response.SpeechRecognitionStarted.Successful)
                    {
                        _logger?.LogInformation("Speech recognition initialization successful");
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
                else if (response.Ping == null)
                {
                    _logger?.LogWarning("Unknown response from PySpeechService: " + JsonSerializer.Serialize(response));
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            _logger?.LogWarning("Connection to PySpeechService closed");
            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        runner.EndProcess();
        
        if (!_hasSentStoppedEvent)
        {
            _hasSentStoppedEvent = true;
            Stopped?.Invoke(this, EventArgs.Empty);
        }
        
        _isSpeechRecognitionSetup = false;
        _isSpeechSetup = false;
        IsConnected = false;
                
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }
        
        if (_speechGrpcService != null)
        {
            await _speechGrpcService.RequestStream.CompleteAsync();
        }
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }
    }

    private async Task Monitor(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _speechGrpcService != null)
        {
            var diff = DateTime.Now - _lastResponseTime;
            if (diff?.TotalSeconds > 90)
            {
                await CleanupAsync();
            }
            else if (diff == null || diff.Value.TotalSeconds > 25)
            {
                await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
                {
                    Ping = new PingRequest
                    {
                        Time = DateTime.Now.ToLongTimeString()
                    }
                }, cancellationToken);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
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

        // Make 3 attempts to send the first ping request and receive a response
        for (var i = 0; i < 3; i++)
        {
            var sendSuccessful = await SendSpeechServiceRequestAsync(new SpeechServiceRequest()
            {
                Ping = new PingRequest
                {
                    Time = DateTime.Now.ToLongTimeString()
                }
            }, cancellationToken, true);

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
    
    private async Task<bool> SendSpeechServiceRequestAsync(SpeechServiceRequest request, CancellationToken cancellationToken = default, bool isInitialRequest = false)
    {
        if (_speechGrpcService == null || (!isInitialRequest && !IsConnected))
        {
            return false;
        }

        try
        {
            await _speechGrpcService.RequestStream.WriteAsync(request, cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to make PySpeechService call {Call}", JsonSerializer.Serialize(request));
            return false;
        }
    }
}