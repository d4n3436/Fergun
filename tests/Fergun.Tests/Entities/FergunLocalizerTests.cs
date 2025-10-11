using System.Globalization;
using System.Linq;
using Fergun.Localization;
using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace Fergun.Tests.Entities;

public class FergunLocalizerTests
{
    [Fact]
    public void FergunLocalizer_Index_Accessor_Returns_Localized_String()
    {
        var localizerMock = CreateMockedLocalizer<FergunLocalizerTests>("Resource", "Value {0}");
        var sharedLocalizerMock = CreateMockedLocalizer<SharedResource>("SharedResource", "Shared Value {0}");

        var fergunLocalizer = new FergunLocalizer<FergunLocalizerTests>(localizerMock.Object, sharedLocalizerMock.Object);

        var localizedString = localizerMock.Object["Resource"];
        var actualLocalizedString = fergunLocalizer["Resource"];

        Assert.Equal(localizedString.Name, actualLocalizedString.Name);
        Assert.Equal(localizedString.Value, actualLocalizedString.Value);

        localizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.AtLeastOnce());
        sharedLocalizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.Never());

        var sharedLocalizedString = sharedLocalizerMock.Object["SharedResource"];
        var actualSharedLocalizedString = fergunLocalizer["SharedResource"];

        Assert.Equal(sharedLocalizedString.Name, actualSharedLocalizedString.Name);
        Assert.Equal(sharedLocalizedString.Value, actualSharedLocalizedString.Value);

        sharedLocalizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.AtLeastOnce());
    }

    [Fact]
    public void FergunLocalizer_Index_Accessor_With_Parameters_Returns_Localized_String()
    {
        var localizerMock = CreateMockedLocalizer<FergunLocalizerTests>("Resource2", "Value 2 {0}");
        var sharedLocalizerMock = CreateMockedLocalizer<SharedResource>("SharedResource2", "Shared Value 2 {0}");

        var fergunLocalizer = new FergunLocalizer<FergunLocalizerTests>(localizerMock.Object, sharedLocalizerMock.Object);

        var localizedString = localizerMock.Object["Resource2", "test"];
        var actualLocalizedString = fergunLocalizer["Resource2", "test"];

        Assert.Equal(localizedString.Name, actualLocalizedString.Name);
        Assert.Equal(localizedString.Value, actualLocalizedString.Value);

        localizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.AtLeastOnce());
        sharedLocalizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.Never());

        var sharedLocalizedString = sharedLocalizerMock.Object["SharedResource2", "test"];
        var actualSharedLocalizedString = fergunLocalizer["SharedResource2", "test"];

        Assert.Equal(sharedLocalizedString.Name, actualSharedLocalizedString.Name);
        Assert.Equal(sharedLocalizedString.Value, actualSharedLocalizedString.Value);

        sharedLocalizerMock.Verify(s => s[It.IsAny<string>(), It.IsAny<object[]>()], Times.AtLeastOnce());
    }

    [Fact]
    public void FergunLocalizer_CurrentCulture_Has_Expected_Value()
    {
        var localizer = Mock.Of<IStringLocalizer<FergunLocalizerTests>>();
        var sharedLocalizer = Mock.Of<IStringLocalizer<SharedResource>>();

        var fergunLocalizer = new FergunLocalizer<FergunLocalizerTests>(localizer, sharedLocalizer);

        Assert.Equal(fergunLocalizer.CurrentCulture, FergunLocalizer.DefaultCulture);

        fergunLocalizer.CurrentCulture = CultureInfo.GetCultureInfo("es");

        Assert.Equal(fergunLocalizer.CurrentCulture, CultureInfo.GetCultureInfo("es"));
    }

    [Fact]
    public void FergunLocalizer_GetAllStrings_Have_Expected_Values()
    {
        var localizerMock = CreateMockedLocalizer<FergunLocalizerTests>("Resource", "Value {0}");
        var sharedLocalizerMock = CreateMockedLocalizer<SharedResource>("SharedResource", "Shared Value {0}");

        var fergunLocalizer = new FergunLocalizer<FergunLocalizerTests>(localizerMock.Object, sharedLocalizerMock.Object);

        var strings = fergunLocalizer.GetAllStrings().ToArray();
        var actualStrings = new[]
        {
            new LocalizedString("Resource", "Value {0}"),
            new LocalizedString("SharedResource", "Shared Value {0}")
        };

        Assert.All(strings, [AssertionMethod] (localized, index) =>
        {
            Assert.Equal(localized.Name, actualStrings[index].Name);
            Assert.Equal(localized.Value, actualStrings[index].Value);
        });
    }

    private static Mock<IStringLocalizer<T>> CreateMockedLocalizer<T>(string name, string value)
    {
        var localizerMock = new Mock<IStringLocalizer<T>>();

        localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns<string>(s => s == name
                ? new LocalizedString(s, value)
                : new LocalizedString(s, string.Empty, true));

        localizerMock
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns<string, object[]>((s, p) =>
            {
                if (s == name)
                    return new LocalizedString(s, p.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, p));
                return new LocalizedString(s, string.Empty, true);
            });

        localizerMock.Setup(x => x.GetAllStrings(It.IsAny<bool>()))
            .Returns(() => [new LocalizedString(name, value)]);

        localizerMock.SetupAllProperties();

        return localizerMock;
    }
}