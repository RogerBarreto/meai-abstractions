
using AssemblyAI;
using AssemblyAI.Transcripts;
using MEAI.Abstractions;

namespace ConsoleAssemblyAI;

internal sealed partial class AssemblyAITranscriptionClient : IAudioTranscriptionClient
{
    private readonly string _apiKey;
    private AssemblyAIClient _client;

    public AssemblyAITranscriptionClient(string apiKey)
    {
        this._apiKey = apiKey;
        this._client = new AssemblyAIClient(apiKey);
    }

    public void Dispose()
    {
    }

    private TranscriptOptionalParams ToTranscriptOptionalParams(AudioTranscriptionOptions? transcriptionOptions)
    {
        TranscriptOptionalParams request = new();

        if (transcriptionOptions is null)
        {
            return request;
        }

        request.LanguageCode = ToSourceLanguage(transcriptionOptions.SourceLanguage);

        return request;

        TranscriptLanguageCode? ToSourceLanguage(string? sourceLanguage)
        {
            if (sourceLanguage == null)
            {
                return null;
            }

            return sourceLanguage.ToUpperInvariant() switch
            {
                "PT" => TranscriptLanguageCode.Pt,
                _ => TranscriptLanguageCode.EnUs,
            };
        }
    }

    private TranscriptionCompletion ToTranscriptionCompletion(Transcript transcript)
    {
        return new()
        {
            RawRepresentation = transcript,
            CompletionId = transcript.Id,
            ModelId = transcript.LanguageModel,
            Content = transcript.Text is not null ? new(transcript.Text) : null
        };
    }
}
