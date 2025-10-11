using System;
using System.Threading.Tasks;
using Fergun.Common;
using GTranslate;
using GTranslate.Translators;
using Moq;
using Xunit;

namespace Fergun.Tests.Entities;

public class FergunTranslatorTests
{
    private readonly FergunTranslator _fergunTranslator;
    private readonly Mock<ITranslator> _innerTranslatorMock;

    public FergunTranslatorTests()
    {
        _innerTranslatorMock = new Mock<ITranslator>();
        _innerTranslatorMock.Setup(x => x.IsLanguageSupported(It.IsAny<string>()))
            .Returns(true);

        _innerTranslatorMock.Setup(x => x.IsLanguageSupported(It.IsAny<ILanguage>()))
            .Returns(true);

        _fergunTranslator = new FergunTranslator([_innerTranslatorMock.Object, Mock.Of<ITranslator>()]);
    }

    [Fact]
    public void FergunTranslator_Name_Returns_Type_Name()
    {
        Assert.Equal(nameof(FergunTranslator), _fergunTranslator.Name);
    }

    [Fact]
    public void FergunTranslator_Randomize_Shuffles_Translators()
    {
        _fergunTranslator.Randomize(new Random(0));

        Assert.NotSame(_innerTranslatorMock.Object, _fergunTranslator._translators[0]);
    }

    [Fact]
    public async Task FergunTranslator_TranslateAsync_Calls_Wrapper_Method()
    {
        await _fergunTranslator.TranslateAsync(string.Empty, "en");
        await _fergunTranslator.TranslateAsync(string.Empty, Language.GetLanguage("en"));

        _innerTranslatorMock.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once());
        _innerTranslatorMock.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<ILanguage>(), It.IsAny<ILanguage?>()), Times.Once());
    }

    [Fact]
    public async Task FergunTranslator_TransliterateAsync_Calls_Wrapper_Method()
    {
        await _fergunTranslator.TransliterateAsync(string.Empty, "en");
        await _fergunTranslator.TransliterateAsync(string.Empty, Language.GetLanguage("en"));

        _innerTranslatorMock.Verify(x => x.TransliterateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never());
        _innerTranslatorMock.Verify(x => x.TransliterateAsync(It.IsAny<string>(), It.IsAny<ILanguage>(), It.IsAny<ILanguage?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task FergunTranslator_DetectLanguageAsync_Calls_Wrapper_Method()
    {
        await _fergunTranslator.DetectLanguageAsync(string.Empty);

        _innerTranslatorMock.Verify(x => x.DetectLanguageAsync(It.IsAny<string>()), Times.Once());
    }

    [Fact]
    public void FergunTranslator_IsLanguageSupported_Calls_Wrapper_Method()
    {
        _fergunTranslator.IsLanguageSupported(It.IsAny<string>());
        _fergunTranslator.IsLanguageSupported(It.IsAny<ILanguage>());

        _innerTranslatorMock.Verify(x => x.IsLanguageSupported(It.IsAny<string>()), Times.Once());
        _innerTranslatorMock.Verify(x => x.IsLanguageSupported(It.IsAny<ILanguage>()), Times.Once());
    }
}