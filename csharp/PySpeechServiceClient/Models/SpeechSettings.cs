namespace PySpeechServiceClient.Models;

/// <summary>
/// Settings to use for PySpeechService's TTS
/// </summary>
public class SpeechSettings
{
    /// <summary>
    /// Piper model name to use for the main TTS voice
    /// </summary>
    public string ModelName { get; set; } = "hfc_female";
    
    /// <summary>
    /// Path to a Piper model onnx file to use for the main TTS voice
    /// </summary>
    public string OnnxPath { get; set; } = "";
    
    /// <summary>
    /// Path to a Piper model config json file to use for the main TTS voice
    /// </summary>
    public string ConfigPath { get; set; } = "";
    
    /// <summary>
    /// Piper model name to use for the alternate TTS voice
    /// </summary>
    public string AltModelName { get; set; } = "hfc_male";
    
    /// <summary>
    /// Path to a Piper model onnx file to use for the alternate TTS voice
    /// </summary>
    public string AltOnnxPath { get; set; } = "";
    
    /// <summary>
    /// Path to a Piper model config json file to use for the alternate TTS voice
    /// </summary>
    public string AltConfigPath { get; set; } = "";
    
    /// <summary>
    /// The speed to use for TTS. Greater than 1 is faster and less than 1 is slower. 
    /// </summary>
    public double Speed { get; set; } = 1;
    
    /// <summary>
    /// The value to modify the volume of the TTS response
    /// </summary>
    public double Gain { get; set; }
    
    /// <summary>
    /// How to modify the pitch of the TTS response. Greater than 1 increases the pitch and
    /// less than 1 lowers the pitch.
    /// </summary>
    public double Pitch { get; set; } = 1;

    internal global::SpeechSettings ToSpeechSettings()
    {
        return new global::SpeechSettings()
        {
            ModelName = ModelName,
            OnnxPath = OnnxPath,
            ConfigPath = ConfigPath,
            AltModelName = AltModelName,
            AltOnnxPath = AltOnnxPath,
            AltConfigPath = AltConfigPath,
            Speed = Speed,
            Gain = Gain,
            Pitch = Pitch,
        };
    }
}