using Dawning.Agents.Abstractions.Multimodal;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Multimodal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dawning.Agents.Tests.Multimodal;

public class MultimodalTests
{
    #region ContentItem Tests

    [Fact]
    public void TextContent_Create_ReturnsCorrectType()
    {
        var content = TextContent.Create("Hello World");

        content.Type.Should().Be(ContentType.Text);
        content.Text.Should().Be("Hello World");
    }

    [Fact]
    public void ImageContent_FromUrl_ReturnsCorrectContent()
    {
        var content = ImageContent.FromUrl("https://example.com/image.png", ImageDetail.High);

        content.Type.Should().Be(ContentType.Image);
        content.Url.Should().Be("https://example.com/image.png");
        content.Detail.Should().Be(ImageDetail.High);
        content.Base64Data.Should().BeNull();
    }

    [Fact]
    public void ImageContent_FromBase64_ReturnsCorrectContent()
    {
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var content = ImageContent.FromBase64(base64, "image/jpeg");

        content.Type.Should().Be(ContentType.Image);
        content.Base64Data.Should().Be(base64);
        content.MimeType.Should().Be("image/jpeg");
        content.Url.Should().BeNull();
    }

    [Fact]
    public async Task ImageContent_FromFileAsync_LoadsFile()
    {
        // 创建临时测试文件
        var tempFile = Path.GetTempFileName() + ".png";
        try
        {
            var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG 魔数
            await File.WriteAllBytesAsync(tempFile, testData);

            var content = await ImageContentExtensions.FromFileAsync(tempFile);

            content.Type.Should().Be(ContentType.Image);
            content.Base64Data.Should().Be(Convert.ToBase64String(testData));
            content.MimeType.Should().Be("image/png");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".unknown", "application/octet-stream")]
    public async Task ImageContent_FromFileAsync_DetectsMimeType(
        string extension,
        string expectedMimeType
    )
    {
        var tempFile = Path.GetTempFileName();
        var targetFile = Path.ChangeExtension(tempFile, extension);
        try
        {
            File.Move(tempFile, targetFile);
            await File.WriteAllBytesAsync(targetFile, [1, 2, 3]);

            var content = await ImageContentExtensions.FromFileAsync(targetFile);

            content.MimeType.Should().Be(expectedMimeType);
        }
        finally
        {
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }
        }
    }

    [Fact]
    public void AudioContent_FromBase64_ReturnsCorrectContent()
    {
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var content = AudioContent.FromBase64(base64, "audio/wav");

        content.Type.Should().Be(ContentType.Audio);
        content.Base64Data.Should().Be(base64);
        content.MimeType.Should().Be("audio/wav");
    }

    [Fact]
    public void AudioContent_FromUrl_ReturnsCorrectContent()
    {
        var content = AudioContent.FromUrl("https://example.com/audio.mp3");

        content.Type.Should().Be(ContentType.Audio);
        content.Url.Should().Be("https://example.com/audio.mp3");
    }

    #endregion

    #region MultimodalMessage Tests

    [Fact]
    public void MultimodalMessage_User_CreatesUserMessage()
    {
        var message = MultimodalMessage.User(
            TextContent.Create("描述这张图片"),
            ImageContent.FromUrl("https://example.com/image.png")
        );

        message.Role.Should().Be("user");
        message.Content.Should().HaveCount(2);
        message.Content[0].Should().BeOfType<TextContent>();
        message.Content[1].Should().BeOfType<ImageContent>();
    }

    [Fact]
    public void MultimodalMessage_Assistant_CreatesAssistantMessage()
    {
        var message = MultimodalMessage.Assistant("这是一张猫的图片");

        message.Role.Should().Be("assistant");
        message.Content.Should().HaveCount(1);
        message.Content[0].Should().BeOfType<TextContent>();
        ((TextContent)message.Content[0]).Text.Should().Be("这是一张猫的图片");
    }

    [Fact]
    public void MultimodalMessage_System_CreatesSystemMessage()
    {
        var message = MultimodalMessage.System("你是一个图像分析助手");

        message.Role.Should().Be("system");
        message.Content.Should().HaveCount(1);
    }

    [Fact]
    public void MultimodalMessage_AddText_ChainsCorrectly()
    {
        var message = MultimodalMessage.User().AddText("第一段文本").AddText("第二段文本");

        message.Content.Should().HaveCount(2);
        message.Content.All(c => c is TextContent).Should().BeTrue();
    }

    [Fact]
    public void MultimodalMessage_AddImageUrl_ChainsCorrectly()
    {
        var message = MultimodalMessage
            .User(TextContent.Create("分析这些图片"))
            .AddImageUrl("https://example.com/image1.png")
            .AddImageUrl("https://example.com/image2.png", ImageDetail.High);

        message.Content.Should().HaveCount(3);
        var images = message.Content.OfType<ImageContent>().ToList();
        images.Should().HaveCount(2);
        images[1].Detail.Should().Be(ImageDetail.High);
    }

    [Fact]
    public void MultimodalMessage_AddImageBase64_ChainsCorrectly()
    {
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var message = MultimodalMessage
            .User(TextContent.Create("分析"))
            .AddImageBase64(base64, "image/png");

        message.Content.Should().HaveCount(2);
        var image = message.Content.OfType<ImageContent>().First();
        image.Base64Data.Should().Be(base64);
    }

    #endregion

    #region VisionOptions Tests

    [Fact]
    public void VisionOptions_DefaultValues_AreCorrect()
    {
        var options = new VisionOptions();

        options.Model.Should().BeNull();
        options.Detail.Should().Be(ImageDetail.Auto);
        options.MaxTokens.Should().Be(1024);
        options.Temperature.Should().Be(0.7);
        options.SystemPrompt.Should().BeNull();
    }

    #endregion

    #region VisionAnalysisResult Tests

    [Fact]
    public void VisionAnalysisResult_Ok_CreatesSuccessResult()
    {
        var result = VisionAnalysisResult.Ok("这是一只橙色的猫");

        result.Success.Should().BeTrue();
        result.Description.Should().Be("这是一只橙色的猫");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void VisionAnalysisResult_Fail_CreatesFailureResult()
    {
        var result = VisionAnalysisResult.Fail("图像格式不支持");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("图像格式不支持");
    }

    #endregion

    #region TokenUsage Tests

    [Fact]
    public void TokenUsage_TotalTokens_CalculatesCorrectly()
    {
        var usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 50 };

        usage.TotalTokens.Should().Be(150);
    }

    #endregion

    #region OpenAIVisionProvider Tests

    [Fact]
    public void OpenAIVisionProvider_SupportsVision_ReturnsTrue()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAIVisionProvider(httpClient, "test-key");

        provider.SupportsVision.Should().BeTrue();
    }

    [Fact]
    public void OpenAIVisionProvider_SupportedImageFormats_ContainsCommonFormats()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAIVisionProvider(httpClient, "test-key");

        provider.SupportedImageFormats.Should().Contain("image/png");
        provider.SupportedImageFormats.Should().Contain("image/jpeg");
        provider.SupportedImageFormats.Should().Contain("image/gif");
        provider.SupportedImageFormats.Should().Contain("image/webp");
    }

    [Fact]
    public void OpenAIVisionProvider_MaxImageSize_Is20MB()
    {
        var httpClient = new HttpClient();
        var provider = new OpenAIVisionProvider(httpClient, "test-key");

        provider.MaxImageSize.Should().Be(20 * 1024 * 1024);
    }

    #endregion

    #region DI Extension Tests

    [Fact]
    public void AddOpenAIVision_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIVision("test-api-key");
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<IVisionProvider>();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIVisionProvider>();
    }

    [Fact]
    public void AddOpenAIVision_WithCustomParameters_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddOpenAIVision(
            "test-api-key",
            "https://custom.api.com/v1",
            "gpt-4-vision-preview"
        );
        var sp = services.BuildServiceProvider();

        var provider = sp.GetService<IVisionProvider>();

        provider.Should().NotBeNull();
    }

    #endregion

    #region DetectedObject Tests

    [Fact]
    public void DetectedObject_WithBoundingBox_HasCorrectValues()
    {
        var obj = new DetectedObject
        {
            Name = "cat",
            Confidence = 0.95,
            BoundingBox = new BoundingBox
            {
                X = 0.1,
                Y = 0.2,
                Width = 0.3,
                Height = 0.4,
            },
        };

        obj.Name.Should().Be("cat");
        obj.Confidence.Should().Be(0.95);
        obj.BoundingBox!.X.Should().Be(0.1);
        obj.BoundingBox.Y.Should().Be(0.2);
        obj.BoundingBox.Width.Should().Be(0.3);
        obj.BoundingBox.Height.Should().Be(0.4);
    }

    #endregion

    #region ImageDetail Tests

    [Theory]
    [InlineData(ImageDetail.Auto)]
    [InlineData(ImageDetail.Low)]
    [InlineData(ImageDetail.High)]
    public void ImageDetail_AllValues_CanBeUsed(ImageDetail detail)
    {
        var content = ImageContent.FromUrl("https://example.com/image.png", detail);

        content.Detail.Should().Be(detail);
    }

    #endregion

    #region ContentType Tests

    [Theory]
    [InlineData(ContentType.Text)]
    [InlineData(ContentType.Image)]
    [InlineData(ContentType.Audio)]
    [InlineData(ContentType.Video)]
    [InlineData(ContentType.Document)]
    public void ContentType_AllValues_Exist(ContentType type)
    {
        Enum.IsDefined(typeof(ContentType), type).Should().BeTrue();
    }

    #endregion
}
