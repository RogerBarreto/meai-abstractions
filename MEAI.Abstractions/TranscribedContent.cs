
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;
public class TranscribedContent : AIContent
{
    public TranscribedContent(string transcription)
    {
        this.Transcription = transcription;
    }

    public string Transcription { get; set; }
}
