namespace PySpeechServiceClient.Models;

public class SpeechSettings
{
    public string ModelName { get; set; } = "hfc_female";
    public string OnnxPath { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string AltModelName { get; set; } = "hfc_male";
    public string AltOnnxPath { get; set; } = "";
    public string AltConfigPath { get; set; } = "";
    public double Speed { get; set; } = 1;
    public double Gain { get; set; }
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