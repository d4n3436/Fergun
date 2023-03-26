using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Fergun.Modules;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class SharedModuleTests
{
    private readonly Mock<IFergunTranslator> _translatorMock = new();
    private readonly Mock<SharedModule> _sharedModuleMock;
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly Mock<IComponentInteraction> _componentInteractionMock = new();

    public SharedModuleTests()
    {
        var localizer = Utils.CreateMockedLocalizer<SharedResource>();
        _sharedModuleMock = new Mock<SharedModule>(() => new SharedModule(Mock.Of<ILogger<SharedModule>>(), localizer, _translatorMock.Object, new GoogleTranslator2()));
        _translatorMock.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync<string, string, string?, IFergunTranslator, ITranslationResult>((text, target, source) =>
            {
                if (text == "Error")
                {
                    throw new ArgumentException(null, nameof(text));
                }

                var targetLanguage = Language.GetLanguage(target);
                Language.TryGetLanguage(source ?? string.Empty, out var sourceLanguage);

                var mock = new Mock<ITranslationResult>();
                mock.SetupGet(x => x.Translation).Returns(text);
                mock.SetupGet(x => x.Source).Returns(text);
                mock.SetupGet(x => x.SourceLanguage).Returns(sourceLanguage ?? targetLanguage);
                mock.SetupGet(x => x.TargetLanguage).Returns(targetLanguage);
                mock.SetupGet(x => x.Service).Returns(text switch
                {
                    "Bing" => "BingTranslator",
                    "Microsoft" => "MicrosoftTranslator",
                    "Yandex" => "YandexTranslator",
                    _ => "GoogleTranslator"
                });

                return mock.Object;
            });

        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");
        _componentInteractionMock.SetupGet(x => x.UserLocale).Returns("en");
    }

    [Theory]
    [InlineData(null, "en", null, false)]
    [InlineData("text", "", "es", false)]
    [InlineData("text", "en", "foobar", true)]
    [InlineData("Google", "en", "es", false)]
    [InlineData("Bing", "en", null, true)]
    [InlineData("Microsoft", "tr", "es", true)]
    [InlineData("Yandex", "ru", "en", false)]
    [InlineData("Error", "fr", "it", true)]
    public async Task TranslateAsync_Returns_Results_Or_Fails_Preconditions(string text, string target, string? source, bool ephemeral)
    {
        var result = await _sharedModuleMock.Object.TranslateAsync(_interactionMock.Object, text, target, source, ephemeral);

        _interactionMock.VerifyGet(x => x.UserLocale);

        bool passedPreconditions = !string.IsNullOrEmpty(text) && Language.TryGetLanguage(target, out _) && (source == null || Language.TryGetLanguage(source, out _));

        if (text != "Error")
        {
            Assert.Equal(result.IsSuccess, passedPreconditions);
        }

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), passedPreconditions ? Times.Once : Times.Never);

        _translatorMock.Verify(x => x.TranslateAsync(It.Is<string>(s => s == text), It.Is<string>(s => s == target), It.Is<string>(s => s == source)), passedPreconditions ? Times.Once : Times.Never);

        _interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), result.IsSuccess && passedPreconditions ? Times.Once : Times.Never);
    }

    [Theory]
    [InlineData("Microsoft", "tr", "es", true)]
    [InlineData("Yandex", "ru", "en", false)]
    public async Task TranslateAsync_Uses_DeferLoadingAsync(string text, string target, string? source, bool ephemeral)
    {
        var result = await _sharedModuleMock.Object.TranslateAsync(_componentInteractionMock.Object, text, target, source, ephemeral);

        Assert.True(result.IsSuccess);

        _componentInteractionMock.VerifyGet(x => x.UserLocale);

        _componentInteractionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), Times.Never);
        _componentInteractionMock.Verify(x => x.DeferLoadingAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), Times.Once);

        _translatorMock.Verify(x => x.TranslateAsync(It.Is<string>(s => s == text), It.Is<string>(s => s == target), It.Is<string>(s => s == source)), Times.Once);

        _componentInteractionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Theory]
    [InlineData("", "en", true)]
    [InlineData("test", "foobar", false)]
    [InlineData("test", "yo", true)]
    [InlineData("Hello world", "en", true)]
    [InlineData("Hola mundo", "es", false)]
    public async Task TtsAsync_Sends_Results_Or_Fails_Preconditions(string text, string target, bool ephemeral)
    {
        var result = await _sharedModuleMock.Object.GoogleTtsAsync(_interactionMock.Object, text, target, ephemeral);

        _interactionMock.VerifyGet(x => x.UserLocale);

        bool passedPreconditions = !string.IsNullOrWhiteSpace(text) && Language.TryGetLanguage(target, out var language) && GoogleTranslator2.TextToSpeechLanguages.Contains(language);
        Assert.Equal(passedPreconditions, result.IsSuccess);

        _interactionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), passedPreconditions ? Times.Once : Times.Never);

        _interactionMock.Verify(x => x.FollowupWithFileAsync(It.Is<FileAttachment>(f => f.FileName == "tts.mp3"), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), passedPreconditions ? Times.Once : Times.Never);
    }

    [Theory]
    [InlineData("Привет, мир", "ru", true)]
    [InlineData("Bonjour le monde", "fr", false)]
    public async Task TtsAsync_Uses_DeferLoadingAsync(string text, string target, bool ephemeral)
    {
        await _sharedModuleMock.Object.GoogleTtsAsync(_componentInteractionMock.Object, text, target, ephemeral);

        _componentInteractionMock.VerifyGet(x => x.UserLocale);

        _componentInteractionMock.Verify(x => x.DeferAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), Times.Never);
        _componentInteractionMock.Verify(x => x.DeferLoadingAsync(It.Is<bool>(b => b == ephemeral), It.IsAny<RequestOptions>()), Times.Once);

        _componentInteractionMock.Verify(x => x.FollowupWithFileAsync(It.Is<FileAttachment>(f => f.FileName == "tts.mp3"), It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.Is<bool>(b => b == ephemeral),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }
}