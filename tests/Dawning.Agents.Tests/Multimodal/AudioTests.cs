using Dawning.Agents.Abstractions.Multimodal;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Multimodal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Multimodal;

public class AudioTests
{
    private static OpenAIWhisperProvider CreateWhisperProvider(HttpClient? httpClient = null)
    {
        var client = httpClient ?? new HttpClient();
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        return new OpenAIWhisperProvider(client, factoryMock.Object, "test-key");
    }

    #region TranscriptionOptions Tests

    [Fact]
    public void TranscriptionOptions_DefaultValues_AreCorrect()
    {
        var options = new TranscriptionOptions();

        options.Model.Should().BeNull();
        options.Language.Should().BeNull();
        options.Prompt.Should().BeNull();
        options.ResponseFormat.Should().Be(TranscriptionFormat.Text);
        options.Temperature.Should().Be(0);
        options.IncludeTimestamps.Should().BeFalse();
        options.TimestampGranularity.Should().Be(TimestampGranularity.Segment);
    }

    [Fact]
    public void TranscriptionOptions_SectionName_IsCorrect()
    {
        TranscriptionOptions.SectionName.Should().Be("Audio:Transcription");
    }

    #endregion

    #region TranscriptionResult Tests

    [Fact]
    public void TranscriptionResult_Ok_CreatesSuccessResult()
    {
        var result = TranscriptionResult.Ok("Hello world", "en", 5.5);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("Hello world");
        result.DetectedLanguage.Should().Be("en");
        result.DurationSeconds.Should().Be(5.5);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void TranscriptionResult_Failed_CreatesFailureResult()
    {
        var result = TranscriptionResult.Failed("File not found");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("File not found");
        result.Text.Should().BeNull();
    }

    [Fact]
    public void TranscriptionResult_WithSegments_HasCorrectStructure()
    {
        var result = new TranscriptionResult
        {
            Success = true,
            Text = "Hello world. How are you?",
            Segments =
            [
                new TranscriptionSegment
                {
                    Id = 0,
                    Start = 0.0,
                    End = 1.5,
                    Text = "Hello world.",
                    Confidence = 0.95,
                },
                new TranscriptionSegment
                {
                    Id = 1,
                    Start = 1.5,
                    End = 3.0,
                    Text = "How are you?",
                    Confidence = 0.92,
                },
            ],
        };

        result.Segments.Should().HaveCount(2);
        result.Segments![0].Start.Should().Be(0.0);
        result.Segments![1].End.Should().Be(3.0);
    }

    [Fact]
    public void TranscriptionResult_WithWords_HasCorrectStructure()
    {
        var result = new TranscriptionResult
        {
            Success = true,
            Text = "Hello world",
            Words =
            [
                new TranscriptionWord
                {
                    Word = "Hello",
                    Start = 0.0,
                    End = 0.5,
                    Confidence = 0.98,
                },
                new TranscriptionWord
                {
                    Word = "world",
                    Start = 0.6,
                    End = 1.0,
                    Confidence = 0.95,
                },
            ],
        };

        result.Words.Should().HaveCount(2);
        result.Words![0].Word.Should().Be("Hello");
        result.Words![1].Word.Should().Be("world");
    }

    #endregion

    #region TranscriptionFormat Tests

    [Theory]
    [InlineData(TranscriptionFormat.Text)]
    [InlineData(TranscriptionFormat.Json)]
    [InlineData(TranscriptionFormat.VerboseJson)]
    [InlineData(TranscriptionFormat.Srt)]
    [InlineData(TranscriptionFormat.Vtt)]
    public void TranscriptionFormat_AllValues_Exist(TranscriptionFormat format)
    {
        Enum.IsDefined(typeof(TranscriptionFormat), format).Should().BeTrue();
    }

    #endregion

    #region SpeechOptions Tests

    [Fact]
    public void SpeechOptions_DefaultValues_AreCorrect()
    {
        var options = new SpeechOptions();

        options.Model.Should().BeNull();
        options.Voice.Should().Be("alloy");
        options.OutputFormat.Should().Be(SpeechOutputFormat.Mp3);
        options.Speed.Should().Be(1.0);
        options.Pitch.Should().BeNull();
        options.Volume.Should().BeNull();
    }

    [Fact]
    public void SpeechOptions_SectionName_IsCorrect()
    {
        SpeechOptions.SectionName.Should().Be("Audio:Speech");
    }

    #endregion

    #region SpeechResult Tests

    [Fact]
    public void SpeechResult_Ok_CreatesSuccessResult()
    {
        var audioData = new byte[] { 1, 2, 3, 4, 5 };
        var result = SpeechResult.Ok(audioData, 2.5);

        result.Success.Should().BeTrue();
        result.AudioData.Should().BeEquivalentTo(audioData);
        result.DurationSeconds.Should().Be(2.5);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SpeechResult_OkFile_CreatesFileResult()
    {
        var result = SpeechResult.OkFile("/path/to/output.mp3", 3.0);

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be("/path/to/output.mp3");
        result.DurationSeconds.Should().Be(3.0);
    }

    [Fact]
    public void SpeechResult_Failed_CreatesFailureResult()
    {
        var result = SpeechResult.Failed("API rate limit exceeded");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("API rate limit exceeded");
        result.AudioData.Should().BeNull();
    }

    #endregion

    #region SpeechOutputFormat Tests

    [Theory]
    [InlineData(SpeechOutputFormat.Mp3)]
    [InlineData(SpeechOutputFormat.Opus)]
    [InlineData(SpeechOutputFormat.Aac)]
    [InlineData(SpeechOutputFormat.Flac)]
    [InlineData(SpeechOutputFormat.Wav)]
    [InlineData(SpeechOutputFormat.Pcm)]
    public void SpeechOutputFormat_AllValues_Exist(SpeechOutputFormat format)
    {
        Enum.IsDefined(typeof(SpeechOutputFormat), format).Should().BeTrue();
    }

    #endregion

    #region VoiceInfo Tests

    [Fact]
    public void VoiceInfo_HasCorrectStructure()
    {
        var voice = new VoiceInfo
        {
            Id = "nova",
            Name = "Nova",
            Gender = VoiceGender.Female,
            Languages = ["en", "zh"],
            Description = "Friendly voice",
            PreviewUrl = "https://example.com/preview.mp3",
        };

        voice.Id.Should().Be("nova");
        voice.Name.Should().Be("Nova");
        voice.Gender.Should().Be(VoiceGender.Female);
        voice.Languages.Should().Contain("en");
        voice.Languages.Should().Contain("zh");
        voice.Description.Should().Be("Friendly voice");
    }

    [Theory]
    [InlineData(VoiceGender.Neutral)]
    [InlineData(VoiceGender.Male)]
    [InlineData(VoiceGender.Female)]
    public void VoiceGender_AllValues_Exist(VoiceGender gender)
    {
        Enum.IsDefined(typeof(VoiceGender), gender).Should().BeTrue();
    }

    #endregion

    #region OpenAIWhisperProvider Tests

    [Fact]
    public void OpenAIWhisperProvider_NullHttpClient_ShouldThrow()
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        var act = () => new OpenAIWhisperProvider(null!, factoryMock.Object, "test-key");
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void OpenAIWhisperProvider_NullHttpClientFactory_ShouldThrow()
    {
        var act = () => new OpenAIWhisperProvider(new HttpClient(), null!, "test-key");
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClientFactory");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void OpenAIWhisperProvider_InvalidApiKey_ShouldThrow(string? apiKey)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        var act = () => new OpenAIWhisperProvider(new HttpClient(), factoryMock.Object, apiKey!);
        act.Should().Throw<ArgumentException>().WithParameterName("apiKey");
    }

    [Fact]
    public void OpenAIWhisperProvider_Name_IsCorrect()
    {
        var provider = CreateWhisperProvider();

        provider.Name.Should().Be("OpenAI-Whisper");
    }

    [Fact]
    public void OpenAIWhisperProvider_SupportedFormats_ContainsCommonFormats()
    {
        var provider = CreateWhisperProvider();

        provider.SupportedFormats.Should().Contain("mp3");
        provider.SupportedFormats.Should().Contain("mp4");
        provider.SupportedFormats.Should().Contain("wav");
        provider.SupportedFormats.Should().Contain("webm");
        provider.SupportedFormats.Should().Contain("flac");
    }

    [Fact]
    public void OpenAIWhisperProvider_MaxFileSize_Is25MB()
    {
        var provider = CreateWhisperProvider();

        provider.MaxFileSize.Should().Be(25 * 1024 * 1024);
    }

    [Fact]
    public void OpenAIWhisperProvider_MaxDurationSeconds_Is2Hours()
    {
        var provider = CreateWhisperProvider();

        provider.MaxDurationSeconds.Should().Be(7200);
    }

    [Fact]
    public async Task OpenAIWhisperProvider_TranscribeAsync_WithEmptyAudio_ReturnsFailed()
    {
        var provider = CreateWhisperProvider();

        var emptyAudio = new AudioContent();
        var result = await provider.TranscribeAsync(emptyAudio);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Audio content is empty");
    }

    [Fact]
    public async Task OpenAIWhisperProvider_TranscribeFileAsync_WithNonexistentFile_ReturnsFailed()
    {
        var provider = CreateWhisperProvider();

        var result = await provider.TranscribeFileAsync("/nonexistent/file.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task OpenAIWhisperProvider_TranscribeFileAsync_WithUnsupportedFormat_ReturnsFailed()
    {
        var tempFile = Path.GetTempFileName() + ".xyz";
        try
        {
            await File.WriteAllBytesAsync(tempFile, [1, 2, 3]);

            var provider = CreateWhisperProvider();

            var result = await provider.TranscribeFileAsync(tempFile);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("Unsupported format");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region OpenAITTSProvider Tests

    [Fact]
    public void OpenAITTSProvider_NullHttpClient_ShouldThrow()
    {
        var act = () => new OpenAITTSProvider(null!, "test-key");
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void OpenAITTSProvider_InvalidApiKey_ShouldThrow(string? apiKey)
    {
        var act = () => new OpenAITTSProvider(new HttpClient(), apiKey!);
        act.Should().Throw<ArgumentException>().WithParameterName("apiKey");
    }

    [Fact]
    public void OpenAITTSProvider_Name_IsCorrect()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        provider.Name.Should().Be("OpenAI-TTS");
    }

    [Fact]
    public void OpenAITTSProvider_AvailableVoices_ContainsAllVoices()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        provider.AvailableVoices.Should().HaveCount(6);

        var voiceIds = provider.AvailableVoices.Select(v => v.Id).ToList();
        voiceIds.Should().Contain("alloy");
        voiceIds.Should().Contain("echo");
        voiceIds.Should().Contain("fable");
        voiceIds.Should().Contain("onyx");
        voiceIds.Should().Contain("nova");
        voiceIds.Should().Contain("shimmer");
    }

    [Fact]
    public void OpenAITTSProvider_SupportedOutputFormats_ContainsAllFormats()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        provider.SupportedOutputFormats.Should().Contain("mp3");
        provider.SupportedOutputFormats.Should().Contain("opus");
        provider.SupportedOutputFormats.Should().Contain("aac");
        provider.SupportedOutputFormats.Should().Contain("flac");
        provider.SupportedOutputFormats.Should().Contain("wav");
        provider.SupportedOutputFormats.Should().Contain("pcm");
    }

    [Fact]
    public void OpenAITTSProvider_MaxTextLength_Is4096()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        provider.MaxTextLength.Should().Be(4096);
    }

    [Fact]
    public async Task OpenAITTSProvider_SynthesizeAsync_WithEmptyText_ReturnsFailed()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        var result = await provider.SynthesizeAsync("");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Text content is empty");
    }

    [Fact]
    public async Task OpenAITTSProvider_SynthesizeAsync_WithTooLongText_ReturnsFailed()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAITTSProvider(httpClient, "test-key");

        var longText = new string('a', 5000);
        var result = await provider.SynthesizeAsync(longText);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Text too long");
    }

    #endregion

    #region DI Extension Tests

    [Fact]
    public void AddOpenAIWhisper_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIWhisper("test-api-key");
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<IAudioTranscriptionProvider>();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIWhisperProvider>();
    }

    [Fact]
    public void AddOpenAIWhisper_WithCustomParameters_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIWhisper("test-api-key", "https://custom.api.com/v1", "whisper-1");
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<IAudioTranscriptionProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddOpenAITTS_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAITTS("test-api-key");
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<ITextToSpeechProvider>();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAITTSProvider>();
    }

    [Fact]
    public void AddOpenAITTS_WithCustomParameters_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAITTS("test-api-key", "https://custom.api.com/v1", "tts-1-hd");
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<ITextToSpeechProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddOpenAIAudio_RegistersBothProviders()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIAudio("test-api-key");
        var sp = services.BuildServiceProvider();

        var transcriptionProvider = sp.GetService<IAudioTranscriptionProvider>();
        var ttsProvider = sp.GetService<ITextToSpeechProvider>();

        transcriptionProvider.Should().NotBeNull();
        ttsProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddOpenAIMultimodal_RegistersAllProviders()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIMultimodal("test-api-key");
        var sp = services.BuildServiceProvider();

        var visionProvider = sp.GetService<IVisionProvider>();
        var transcriptionProvider = sp.GetService<IAudioTranscriptionProvider>();
        var ttsProvider = sp.GetService<ITextToSpeechProvider>();

        visionProvider.Should().NotBeNull();
        transcriptionProvider.Should().NotBeNull();
        ttsProvider.Should().NotBeNull();
    }

    #endregion

    #region TimestampGranularity Tests

    [Theory]
    [InlineData(TimestampGranularity.Segment)]
    [InlineData(TimestampGranularity.Word)]
    public void TimestampGranularity_AllValues_Exist(TimestampGranularity granularity)
    {
        Enum.IsDefined(typeof(TimestampGranularity), granularity).Should().BeTrue();
    }

    #endregion

    #region TranscriptionSegment Tests

    [Fact]
    public void TranscriptionSegment_HasCorrectStructure()
    {
        var segment = new TranscriptionSegment
        {
            Id = 0,
            Start = 1.5,
            End = 3.5,
            Text = "Hello world",
            Confidence = 0.95,
        };

        segment.Id.Should().Be(0);
        segment.Start.Should().Be(1.5);
        segment.End.Should().Be(3.5);
        segment.Text.Should().Be("Hello world");
        segment.Confidence.Should().Be(0.95);
    }

    #endregion

    #region TranscriptionWord Tests

    [Fact]
    public void TranscriptionWord_HasCorrectStructure()
    {
        var word = new TranscriptionWord
        {
            Word = "hello",
            Start = 0.0,
            End = 0.5,
            Confidence = 0.98,
        };

        word.Word.Should().Be("hello");
        word.Start.Should().Be(0.0);
        word.End.Should().Be(0.5);
        word.Confidence.Should().Be(0.98);
    }

    #endregion
}
