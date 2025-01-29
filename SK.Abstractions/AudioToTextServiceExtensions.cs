using MEAI.Abstractions;
using Microsoft.SemanticKernel.AudioToText;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel;

[Experimental("SKEX0001")]
public static class AudioToTextServiceExtensions
{
    public static IAudioTranscriptionClient ToAudioTranscriptionClient(this IAudioToTextService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return service is IAudioTranscriptionClient client ?
            client :
            new AudioTranscriptionClientAudioToTextService(service);
    }

    public static IAudioToTextService ToAudioToTextService(this IAudioTranscriptionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client is IAudioToTextService service ?
            service :
            new AudioToTextServiceAudioTranscriptionClient(client);
    }
}
