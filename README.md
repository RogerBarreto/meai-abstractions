# Microsoft.Extensions.AI.Abstracitons for Audio-to-Text

This is a proof of concept project to validate the 

`IAudioTranscriptionClient` abstraction against different transcription APIs.

Targeted APIs:

- AssemblyAI
- Whisper.net
- Azure AI Cognitive Speech
- Open AI (WIP)
- Sementic Kernel (IAudioToText) Adapter (WIP)

## Credential Setup (User Secrets)

Use the following commands to set up the user secrets for the cloud based project:

```bash
dotnet user-secrets set "AssemblyAI:ApiKey" "..."
dotnet user-secrets set "OpenAI:ApiKey" "..."
dotnet user-secrets set "AzureAISpeech:SubscriptionKey" "..."
dotnet user-secrets set "AzureAISpeech:Region" "..."
```

## Pre-requisites

### Install SOX

SOX is required to capture audio from the microphone and stream it to the APIs.

- [Download sox for windows](https://sourceforge.net/projects/sox/files/sox/14.4.2/sox-14.4.2-win32.exe/download)
- Install it and add the installation path it to the PATH environment variable. i.e: `C:\Program Files\ffmpeg\`

### Specific for Whisper.net

Whisper.net requires additional setup, as the other APIs are cloud-based.

- Download [CUDA Toolkit Support](https://developer.nvidia.com/cuda-downloads)

	This is required to use the GPU runtime for the Whisper.net API. (Faster and more reliable)

> [!NOTE]
> Whisper models currently only supports 16kHz audio WAVE files.
> FFMPEG is an easy tool to convert the audio files to the required format.
 
- Optional Install FFMPEG
	- [ffmpeg download for windows](https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z)
	- Unzip and save in any folder you desire, ensure you add it to the PATH environment variable. i.e: `C:\Program Files\ffmpeg\`
	- Example command to convert audio file to 16kHz:
		```bash
		ffmpeg -i input.xyz -ar 16000 output.wav
		```

## Usage

Uncomment the desired API in the `Program.cs` file of your target provider and run the project.
