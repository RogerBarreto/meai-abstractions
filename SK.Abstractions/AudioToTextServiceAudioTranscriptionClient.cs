using MEAI.Abstractions;
using Microsoft.SemanticKernel.AudioToText;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel;

[Experimental("SKEX0001")]
internal class AudioToTextServiceAudioTranscriptionClient : IAudioToTextService
{
    private readonly IAudioTranscriptionClient _client;
    public AudioToTextServiceAudioTranscriptionClient(IAudioTranscriptionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this._client = client;
    }

    public IReadOnlyDictionary<string, object?> Attributes => throw new NotImplementedException();

    public async Task<IReadOnlyList<TextContent>> GetTextContentsAsync(AudioContent content, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        Microsoft.Extensions.AI.AudioContent? audioContent = null;
        audioContent = (!content.CanRead) 
            ? audioContent = new(content.Uri!)
            : audioContent = new(data: content.Data!.Value, content.MimeType);

        var completion = await _client.TranscribeAsync(audioContent, cancellationToken: cancellationToken);

        var textContent = new TextContent(completion.Text)
        {
            InnerContent = completion
        };

        return [textContent];
    }
}
