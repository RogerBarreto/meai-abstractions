
using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;
public class AudioTranscribedContent : AIContent
{
    public AudioTranscribedContent(string transcription)
    {
        this.Transcription = transcription;
    }

    public string Transcription { get; set; }
}
