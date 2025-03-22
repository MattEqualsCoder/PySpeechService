namespace PySpeechServiceClient.Models;

public class SpeechRecognitionInitializedEventArgs
{
    /// <summary>
    /// The list of words that are not known to VOSK and will require
    /// some sort of substitute
    /// </summary>
    public ICollection<string>? InvalidSpeechRecognitionWords { get; set; }
}