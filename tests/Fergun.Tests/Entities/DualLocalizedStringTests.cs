using Microsoft.Extensions.Localization;
using Moq;
using System.Globalization;
using System.Threading;
using Xunit;

namespace Fergun.Tests.Entities;

public class DualLocalizedStringTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void DualLocalizedString_Create_Returns_Localized_String(string code)
    {
        const string englishCode = "en";

        var cultureInfo = CultureInfo.GetCultureInfo(code);
        var englishCultureInfo = CultureInfo.GetCultureInfo(englishCode);

        Thread.CurrentThread.CurrentUICulture = cultureInfo;

        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock.Setup(s => s["Hello", It.IsAny<object[]>()]).Returns(() => Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            englishCode => new LocalizedString("Hello", "Hello"),
            _ => new LocalizedString("Hello", "Hola")
        });

        var localizedString = localizerMock.Object["Hello", It.IsAny<object>()];
        var dualLocalizedString = DualLocalizedString.Create(localizerMock.Object, cultureInfo, "Hello");

        Assert.Equal(localizedString.Name, dualLocalizedString.Name);
        Assert.Equal(localizedString.Value, dualLocalizedString.Value);

        if (code == englishCode)
        {
            Assert.Equal(dualLocalizedString.Value, dualLocalizedString.EnglishValue);
        }
        else
        {
            Thread.CurrentThread.CurrentUICulture = englishCultureInfo;
            Assert.Equal(localizerMock.Object["Hello", It.IsAny<object>()].Value, dualLocalizedString.EnglishValue);
        }

        localizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.Exactly(code == englishCode ? 2 : 4));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void DualLocalizedString_Create_With_Arguments_Returns_Localized_String(string code)
    {
        const string englishCode = "en";

        var cultureInfo = CultureInfo.GetCultureInfo(code);
        var englishCultureInfo = CultureInfo.GetCultureInfo(englishCode);

        Thread.CurrentThread.CurrentUICulture = cultureInfo;

        var localizerMock = new Mock<IStringLocalizer>();
        localizerMock.Setup(s => s["Hello", It.IsAny<object[]>()]).Returns(() => Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            englishCode => new LocalizedString("Hello", "Hello test"),
            _ => new LocalizedString("Hello", "Hola test")
        });

        var localizedString = localizerMock.Object["Hello", "test"];
        var dualLocalizedString = DualLocalizedString.Create(localizerMock.Object, cultureInfo, "Hello", "test");

        Assert.Equal(localizedString.Name, dualLocalizedString.Name);
        Assert.Equal(localizedString.Value, dualLocalizedString.Value);

        if (code == englishCode)
        {
            Assert.Equal(dualLocalizedString.Value, dualLocalizedString.EnglishValue);
        }
        else
        {
            Thread.CurrentThread.CurrentUICulture = englishCultureInfo;
            Assert.Equal(localizerMock.Object["Hello", It.IsAny<object>()].Value, dualLocalizedString.EnglishValue);
        }

        localizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.Exactly(code == englishCode ? 2 : 4));
    }
}