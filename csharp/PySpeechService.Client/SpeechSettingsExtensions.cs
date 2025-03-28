namespace PySpeechService.Client;

public static class SpeechSettingsExtensions
{
    internal static SpeechSettings ToSpeechSettings(this TextToSpeech.SpeechSettings settings)
    {
        return new SpeechSettings()
        {
            ModelName = settings.ModelName,
            OnnxPath = settings.OnnxPath,
            ConfigPath = settings.ConfigPath,
            AltModelName = settings.AltModelName,
            AltOnnxPath = settings.AltOnnxPath,
            AltConfigPath = settings.AltConfigPath,
            Speed = settings.Speed,
            Gain = settings.Gain,
            Pitch = settings.Pitch,
        };
    }
}