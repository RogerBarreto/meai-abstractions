# Architecture Decision Record - Audio Transcription (Audio to Text)

## Problem Statement

The project requires the ability to transcribe audio to text. The project is a proof of concept to validate the `IAudioTranscriptionClient` abstraction against different transcription APIs and to provide a consistent interface for the project to use.

> [!NOTE]
> The names used for the proposed abstractions below are open and can be changed at any time given a bigger consensus.

## Considered Options

## Option 1: Generic Multi Modality Abstraction `IModelClient<TInput, TOutput>` (Discarded)

This option would have provided a generic abstraction for all models, including audio transcription. However, this would have made the abstraction too generic and brought up some questioning during the meeting:

### Usability Concerns:

The generic interface could make the API less intuitive and harder to use, as users would not be guided towards the specific options they need. 1

- Naming and Clarity:

    Generic names like "complete streaming" do not convey the specific functionality, making it difficult for users to understand what the method does. Specific names like "transcribe" or "generate song" would be clearer. 2

- Implementation Complexity:

    Implementing a generic interface would still require concrete implementations for each permutation of input and output types, which could be complex and cumbersome. 3

- Specific Use Cases:

    Different services have specific requirements and optimizations for their modalities, which may not be effectively captured by a generic interface. 4

- Future Proofing vs. Practicality:

    While a generic interface aims to be future-proof, it may not be practical for current needs and could lead to an explosion of permutations that are not all relevant. 5

- Separation of Streaming and Non-Streaming:

    There was a concern about separating streaming and non-streaming interfaces, as it could complicate the API further. 6

## Option 2: Audio Transcription Abstraction `IAudioTranscriptionClient` (Preferred)

This option would provide a specific abstraction for audio transcription, which would be more intuitive and easier to use. The specific interface would allow for better optimization and customization for each service.

Initially I thought on having different interfaces for streaming and non-streaming, but after some discussion, it was decided to have a single interface that could handle both scenarios, following the same rational as  `IChatClient`.

> [!NOTE]
> Further modality abstractions will mostly follow this as a standard moving forward.

```csharp
public interface IAudioTranscriptionClient : IDisposable
{
    Task<AudioTranscriptionCompletion> TranscribeAsync(
        IAsyncEnumerable<AudioContent> audioContents, 
        AudioTranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingAudioTranscriptionUpdate> TranscribeStreamingAsync(
        IAsyncEnumerable<AudioContent> audioContents,
        AudioTranscriptionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Inputs:

- `IAsyncEnumerable<AudioContent>`, as a simpler and recent interface, it allows for upload streaming audio content to the service. 
    
    Using this type, also enables usage of large audio files or real-time transcription (without consuming in-memory) and can easily be extended to support different audio input types (`AudioContent` or `Stream` thought extension methods).

    Supporting scenarios like:

    - Single in-memory data of audio. Non up-streaming audio
    - One audio streamed in multiple audio content chunks - Real-time Transcription
    - Single or multiple audio uri (referenced) audioContents - Batch Transcription

    ### AudioContent input extension
    ```csharp
     // single AudioContent extension
     public static Task<AudioTranscriptionCompletion> TranscribeAsync(
     this IAudioTranscriptionClient client,
     AudioContent audioContent, 
     AudioTranscriptionOptions? options = null, 
     CancellationToken cancellationToken = default);
    ```

    ### Stream input extension
    ```csharp
     // Stream extension
     public static Task<AudioTranscriptionCompletion> TranscribeAsync(
     this IAudioTranscriptionClient client,
     Stream audioStream,
     AudioTranscriptionOptions? options = null,
     CancellationToken cancellationToken = default);
    ```

- `AudioTranscriptionOptions`, analogous to existing `ChatOptions` it allows providing additional options on both Streaming and Non-Streaming APIs for the service, such as language, model, or other parameters.



    ```csharp
    public class AudioTranscriptionOptions
    {
        public string? CompletionId { get; set; }

        public string? ModelId { get; set; }

        public string? AudioLanguage { get; set; }

        public int? AudioSampleRate { get; set; }

        public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

        public virtual AudioTranscriptionOptions Clone();
    }
    ```
    - `CompletionId` is a unique identifier for the completion of the transcription. This can be useful while using Non-Streaming API to track the completion status of a specific long-running transcription process (Batch).

        > [!NOTE]
        > Usage of `CompletionId` follows the convention for Chat, we may consider using `TranscriptionId`.

    - `ModelId` is a unique identifier for the model to use for transcription. 
      - [AssemblyAI Models](https://www.assemblyai.com/docs/getting-started/transcribe-an-audio-file#step-4-enable-additional-ai-models) - For features like Speaker Diarization, and more.
      - [OpenAI model](https://platform.openai.com/docs/api-reference/audio/createTranscription#audio-createtranscription-model) - whisper-1.
      - [Azure AI Speech](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/batch-transcription-create?pivots=rest-api#use-a-custom-model) - Custom model when using batch API.

    - `AudioLanguage` is the language of the audio content.
        - `Azure Cognitive Speech` - [Supported languages](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=stt)

    - `AudioSampleRate` is the sample rate of the audio content. Real-time transcription requires a specific sample rate.


### Outputs:


- `AudioTranscriptionCompletion`, For non-streaming API analogous to existing `ChatCompletion` it provides the transcription result and additional information about the transcription.

    ```csharp
    public class AudioTranscriptionCompletion
    {
        [JsonConstructor]
        public AudioTranscriptionCompletion();

        public AudioTranscriptionCompletion(IList<AIContent> contents);

        public AudioTranscriptionCompletion(string? content);

        public string? CompletionId { get; set; }

        public string? ModelId { get; set; }

        [AllowNull]
        public IList<AIContent> Contents

        [JsonIgnore]
        public string? Text => // Gets/Sets in Contents.

        public TimeSpan? StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }

        [JsonIgnore]
        public object? RawRepresentation { get; set; }

        /// <summary>Gets or sets any additional properties associated with the chat completion.</summary>
        public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
    ```

    - `CompletionId` or `TranscriptionId` is a unique identifier for the completion of the transcription. This can be useful while using Non-Streaming API to track the completion status of a specific long-running transcription process (Batch).

        > [!NOTE]
        > Usage of `Completion` as a prefix initially following the convention for ChatCompletion type, this can be changed to `AudioTranscription` or better.

    - `ModelId` is a unique identifier for the model used for transcription. 

    - `Contents` is a list of `AIContent` objects that represent the transcription result. Normally this would be one `TextContent` object that can be retrieved similarly as a `Message` in `ChatCompletion`, but for a batch result, it could have multiple `TextContent` with the transcription texts. 

    - `StartTime` and `EndTime` represents both Timestamps from where the transcription started and ended relative to the audio length. 

        i.e: Audio starts with instrumental music for the first 30 seconds before any speech, the trascription should start from 30 seconds forward, same for the end time.

        > [!NOTE]
        > `TimeSpan` is used to represent the time stamps as it is more intuitive and easier to work with, some services give the time in milliseconds, ticks or other formats.

- `StreamingAudioTranscriptionUpdate`, For streaming API, analogous to existing `StreamingChatCompletionUpdate` it provides the transcription result as multiple chunks of updates, that represents the content generated as well as any important information about the transcription progress.

    ```csharp
        public class StreamingAudioTranscriptionUpdate
        {
            [JsonConstructor]
            public StreamingAudioTranscriptionUpdate()

            public StreamingAudioTranscriptionUpdate(IList<AIContent> contents)

            public StreamingAudioTranscriptionUpdate(string? content)

            public string? CompletionId { get; set; }

            public TimeSpan? StartTime { get; set; }

            public TimeSpan? EndTime { get; set; }

            public required AudioTranscriptionUpdateKind Kind { get; init; }

            public object? RawRepresentation { get; set; }

            public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }

            [JsonIgnore]
            public string? Text => // Gets/Sets in Contents.

            [AllowNull]
            public IList<AIContent> Contents
        }
    ```

    - `CompletionId` or `TranscriptionId` is a unique identifier for the completion of the transcription.

    - `StartTime` and `EndTime` for the given transcribed chunk represents the timestamp where it starts and ends relative to the audio length. 

        i.e: Audio starts with instrumental music for the first 30 seconds before any speech, the transcription chunk will flush with the StartTime from 30 seconds forward until the last word of the chunk which will represent the end time.

        > [!NOTE]
        > `TimeSpan` is used to represent the time stamps as it is more intuitive and easier to work with, some services give the time in milliseconds, ticks or other formats.

    - `Contents` is a list of `AIContent` objects that represent the transcription result. 99% use cases this will be one `TextContent` object that can be retrieved from the `Text` property similarly as a `Text` in `ChatMessage`.
        
    - `Kind` is a `struct` similarly to `ChatRole`

        The decision to use an `struct` similarly to `ChatRole` will allow more flexibility and customization for different API updates where can provide extra update definitions which can be very specific and won't fall much into the general categories described below, this will allow implementers to not skip such updates, providing a more specific `Kind` update.

        ```csharp
        [JsonConverter(typeof(Converter))]
        public readonly struct AudioTranscriptionUpdateKind : IEquatable<AudioTranscriptionUpdateKind>
        {
            public static AudioTranscriptionUpdateKind SessionOpen { get; } = new("sessionopen");
            public static AudioTranscriptionUpdateKind Error { get; } = new("error");
            public static AudioTranscriptionUpdateKind Transcribing { get; } = new("transcribing");
            public static AudioTranscriptionUpdateKind Transcribed { get; } = new("transcribed");
            public static AudioTranscriptionUpdateKind SessionClose { get; } = new("sessionclose");

            // Similar implementation to ChatRole
        }
        ```

        ### General Update Kinds:

        - `Session Open` - When the transcription session is open.
            
        - `Transcribing` - When the transcription is in progress, without waiting for silence. (Preferably for UI updates)
            
            Different apis used different names for this, ie: 
            - AssemblyAI: `PartialTranscriptReceived`
            - Whisper.net: `SegmentData`
            - Azure AI Speech: `RecognizingSpeech`


        - `Transcribed` - When the transcription block is complete after a small period of silence.

            Different API names for this, ie: 
            - AssemblyAI: `FinalTranscriptReceived`
            - Whisper.net: N/A (Not supported by the internal API)
            - Azure AI Speech: `RecognizedSpeech`

        - `Session Close` - When the transcription session is closed.

        - `Error` - When an error occurs during the transcription.
            
            Errors during the streaming can happen, and normally won't block the ongoing process, but can provide more detailed information about the error. For this reason instead of throwing an exception, the error can be provided as part of the ongoing streaming using a dedicated content I'm calling here `ErrorContent`.

            The idea of providing an `ErrorContent` is mainly to avoid using `TextContent` combining the error title,  code and details in a single string, which can be harder to parse and open's a poorer user experience and bad precedent for error handling / error content.

            Similarly to the `UsageContent` in Chat, if an update want to provide a more detailed error information as part of the ongoing streaming, adding the `ErrorContent` that represents the error message, code, and details, may work best for providing more specific error details that are part of an ongoing process.

            ```csharp
            public class ErrorContent : AIContent
            {
                public required string Message { get; set; } // An error must have a message
                public string? Code { get; set; } // Can be non-numerical
                public string? Details { get; set; }
            }
            ```

        Specific API categories:

        - [Azure AI Speech Examples](https://learn.microsoft.com/en-us/dotnet/api/microsoft.cognitiveservices.speech.resultreason?view=azure-dotnet)
        
### Additional Extensions:

### `Stream` -> `ToAsyncEnumerable<T> : where T : DataContent`

This extension method allows converting a `Stream` to an `IAsyncEnumerable<T>` where `T` is a `DataContent` type, this will allow the usage of `Stream` as an input for the `IAudioTranscriptionClient` without the need to load the entire stream into memory and simplifying the usage of the API for majority of mainstream scenarios where `Stream` type is used. 

As we have already extensions for `Stream` this eventually could be dropped but proved to be useful when callers wanted to easily consume a Stream as an `IAsyncEnumerable<T>`.

```csharp
public static class StreamExtensions 
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this Stream audioStream, string? mediaType = null)
        where T : DataContent
    {
        var buffer = new byte[4096];
        while ((await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            yield return (T)Activator.CreateInstance(typeof(T), [(ReadOnlyMemory<byte>)buffer, mediaType])!;
        }
    }
}
```

### `IAsyncEnumerable<T> -> ToStream<T> : where T : DataContent`

Allows converting an `IAsyncEnumerable<T>` to a `Stream` where `T` is a `DataContent` type

This extension will be very useful for implementers of the `IAudioTranscriptionClient` to provide a simple way to convert the `IAsyncEnumerable<T>` to a `Stream` for the underlying service to consume, which majority all of the services SDK's currently support. 

```csharp
public static class IAsyncEnumerableExtensions
{
    public static Stream ToStream<T>(this IAsyncEnumerable<T> stream, T? firstChunk = null, CancellationToken cancellationToken = default) 
        where T : DataContent
        => new DataContentAsyncEnumerableStream<T>(stream, firstChunk, cancellationToken);
}

// Internal class to handle an IAsyncEnumerable<T> as Stream
internal class DataContentAsyncEnumerableStream<T> : Stream 
    where T : DataContent
{
    internal DataContentAsyncEnumerableStream(IAsyncEnumerable<T> asyncEnumerable, T? firstChunk = null, CancellationToken cancellationToken = default)

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```


#### Azure AI Speech SDK - Example
```csharp
public class MyAudioTranscriptionClient : IAudioTranscriptionClient
{
    public async Task<AudioTranscriptionCompletion> TranscribeAsync(
        IAsyncEnumerable<AudioContent> audioContents, 
        AudioTranscriptionOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        using var audioStream = audioContents.ToStream();
        using var audioConfig = AudioConfig.FromStreamInput(audioStream);

        // ...
    }
}
```