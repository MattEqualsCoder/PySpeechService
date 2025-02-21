using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PySpeechServiceClient;

class PySpeechServiceInitResponse
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";
    
    [JsonPropertyName("port")]
    public int Port { get; init; }
}

internal class PySpeechServiceRunner(IServiceProvider serviceProvider) : IDisposable
{
    private readonly ILogger<PySpeechServiceRunner>? _logger = serviceProvider.GetService<ILogger<PySpeechServiceRunner>>();
    private string? _previousOutput;
    private Process? _process;

    public int Port { get; private set; }

    public bool IsRunning => _process?.HasExited == false;

    public event EventHandler? ProcessEnded;

    public async Task<bool> StartAsync()
    {
        Port = 0;
        _previousOutput = null;
        if (_process is { HasExited: false })
        {
            _process.Kill();
            _process.Dispose();
        }

        var commands = GetCommands();

        foreach (var command in commands)
        {
            if (await AttemptCommand(command))
            {
                return true;
            }
        }

        return false;
    }

    private List<(string, string)> GetCommands()
    {
        List<(string, string)> commands = [];

#if DEBUG
        commands.Add(("/home/matt/.cache/pypoetry/virtualenvs/py-speech-service-r00qpw04-py3.12/bin/python", "/home/matt/Source/PySpeechService/python/py_speech_service/__main__.py"));
        _logger?.LogInformation("Adding debug option {Command1} {Command2}", commands[0].Item1, commands[0].Item2);
#endif

        var localAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "py_speech_service");
            
        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(Path.Combine(localAppDataFolder, "py-speech-service.exe")))
            {
                commands.Add((Path.Combine(localAppDataFolder, "py-speech-service.exe"), ""));
            }
            commands.Add(("py-speech-service.exe", ""));
            commands.Add(("py", "-m py-speech-service"));
        }
        else
        {
            if (File.Exists(Path.Combine(localAppDataFolder, "py-speech-service")))
            {
                commands.Add((Path.Combine(localAppDataFolder, "py-speech-service"), ""));
            }
            commands.Add(("py-speech-service", ""));
            commands.Add(("python3", "-m py-speech-service"));
        }

        return commands;
    }

    private async Task<bool> AttemptCommand((string, string) command)
    {
        if (!RunInternalAsync(command, "service"))
        {
            _logger?.LogError("Process could not be started");
            return false;
        }

        // Wait 30 seconds
        for (var i = 0; i < 120; i++)
        {
            if (_process == null || _process.HasExited)
            {
                _logger?.LogError("Process terminated before receiving expected output");
                return false;
            }
            else if (_previousOutput != null)
            {
                return VerifyOutput();
            }
            await Task.Delay(TimeSpan.FromSeconds(.25));
        }

        _logger?.LogError("Timed out waiting for process to start up");
        _process?.Kill();
        _process?.Dispose();
        return false;
    }

    private bool VerifyOutput()
    {
        if (!_previousOutput!.StartsWith('{'))
        {
            _logger?.LogError("Received unexpected response {Response}", _previousOutput);
            return false;
        }

        try
        {
            var response = JsonSerializer.Deserialize<PySpeechServiceInitResponse>(_previousOutput);
            if (response == null)
            {
                _logger?.LogError("Received unexpected response {Response}", _previousOutput);
                return false;
            }

            if (string.IsNullOrEmpty(response.Version) || response.Port == 0)
            {
                _logger?.LogError("Received unexpected response {Response}", _previousOutput);
                return false;
            }
            
            _logger?.LogInformation("PySpeechService {Version} started on port {Port}", $"v{response.Version}", response.Port);
            Port = response.Port;
            return true;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Received unexpected response {Response}", _previousOutput);
            return false;
        }
    }

    private bool RunInternalAsync((string, string) command, string arguments)
    {
        try
        {
            ProcessStartInfo procStartInfo;
            _previousOutput = null;
            
            _logger?.LogInformation("Executing command: {Command}", $"{command.Item1} {command.Item2} {arguments}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                procStartInfo = new ProcessStartInfo("cmd", $"/c {command.Item1} {command.Item2} {arguments}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                procStartInfo = new ProcessStartInfo(command.Item1)
                {
                    Arguments = $"{command.Item2} {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            _process = new Process();
            _process.StartInfo = procStartInfo;
            _process.OutputDataReceived += ProcessOnOutputDataReceived;
            _process.ErrorDataReceived += ProcessOnErrorDataReceived;
            _process.Exited += (_, _) =>
            {
                _process = null;
                ProcessEnded?.Invoke(this, EventArgs.Empty);
            };
            
            if (_process.Start())
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                return true;
            }
            
            return false;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Unknown error running {Command}", $"{command.Item1} {command.Item2} {arguments}");
            return false;
        }
    }

    private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data))
        {
            return;
        }
        _logger?.LogError("Received error from PySpeechService: {Error}", e.Data);
    }

    private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        _previousOutput = e.Data;
        _process?.CancelOutputRead();
    }

    public void EndProcess()
    {
        _process?.Kill();
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        _process?.Kill();
        _process?.Dispose();
    }
}
