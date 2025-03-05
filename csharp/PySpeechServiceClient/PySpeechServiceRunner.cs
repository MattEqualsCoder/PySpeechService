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

internal class DebugJsonFileData
{
    public string? Python { get; set; }
    public string? MainPyFile { get; set; }
}

internal class PySpeechServiceRunner(IServiceProvider serviceProvider) : IDisposable
{
    public static readonly string RequiredPySpeechServiceVersion = "0.0.8";
    
    private readonly ILogger<PySpeechServiceRunner>? _logger = serviceProvider.GetService<ILogger<PySpeechServiceRunner>>();
    private string? _previousOutput;
    private Process? _process;
    private List<string> _ignoredWords = [];

    public int Port { get; private set; }

    public bool IsRunning => _process?.HasExited == false;
    
    public ICollection<string> IgnoredWords => _ignoredWords.ToList();
 
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

        var localAppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "py_speech_service");
        
#if DEBUG
        var debugFilePath = Path.Combine(localAppDataFolder, "debug.json");
        if (File.Exists(debugFilePath))
        {
            var jsonText = File.ReadAllText(debugFilePath);
            var obj = JsonSerializer.Deserialize<DebugJsonFileData>(jsonText);
            if (!string.IsNullOrEmpty(obj?.Python) && !string.IsNullOrEmpty(obj.MainPyFile))
            {
                _logger?.LogInformation("Adding debug option {Command1} {Command2}", obj.Python, obj.MainPyFile);
                commands.Add((obj.Python, obj.MainPyFile));
            }
        }
#endif
            
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
        var arguments = "service";
#if DEBUG
        arguments += " -d";
#endif
        
        if (!RunInternalAsync(command, arguments))
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

            var requiredVersion = VersionStringToInt(RequiredPySpeechServiceVersion)!;
            var version = VersionStringToInt(response.Version);

            if (version >= requiredVersion)
            {
                _logger?.LogInformation("PySpeechService {Version} started on port {Port}", $"v{response.Version}", response.Port);
                Port = response.Port;
                return true;
            }
            else
            {
                _logger?.LogError("Invalid PySpeechService version of v{ResponseVersion}. Version v{RequiredVersion} required.", response.Version, RequiredPySpeechServiceVersion);
                return false;
            }
            
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
            _ignoredWords = [];
            
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

        if (e.Data.Contains("Ignoring word missing in vocabulary"))
        {
            _ignoredWords.Add(e.Data[e.Data.IndexOf('\'')..]);
        }
        else
        {
            _logger?.LogError("Received error from PySpeechService: {Error}", e.Data);
        }
        
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

    private static int? VersionStringToInt(string version)
    {
        var versionParts = version.Split(".");
        if (versionParts.Length != 3)
        {
            return null;
        }

        if (!int.TryParse(versionParts[0], out var partOne) || !int.TryParse(versionParts[1], out var partTwo) ||
            !int.TryParse(versionParts[2], out var partThree))
        {
            return null;
        }
        
        return partOne * 1000000 + partTwo * 1000 + partThree;
    }
}
