namespace ConsoleAssemblyAI;

using Microsoft.Extensions.AI;

public class TranscribedContent : AIContent
{
    public TranscribedContent(string transcription)
    {
        this.Transcription = transcription;
    }

    public string Transcription { get; set; }
}
