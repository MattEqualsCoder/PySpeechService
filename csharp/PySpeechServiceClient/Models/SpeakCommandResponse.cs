namespace PySpeechServiceClient.Models;

public class SpeakCommandResponse
{
    public string FullMessage { get; set; } = "";
    public string CurrentChunk { get; set; } = "";
    public bool IsStartOfMessage { get; set; }
    public bool IsStartOfChunk { get; set; }
    public bool IsEndOfMessage { get; set; }
    public bool IsEndOfChunk { get; set; }
}

public class SpeakCommandResponseEventArgs(SpeakCommandResponse response) : EventArgs
{
    public SpeakCommandResponse Response { get; } = response;
}