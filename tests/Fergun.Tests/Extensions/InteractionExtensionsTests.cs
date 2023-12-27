using System;
using System.Linq;
using Bogus;
using Discord;
using Fergun.Extensions;
using GTranslate;
using Moq;
using Xunit;

namespace Fergun.Tests.Extensions;

public class InteractionExtensionsTests
{
    [Theory]
    [MemberData(nameof(GetLocales))]
    [MemberData(nameof(GetRandomStrings))]
    public void Interaction_GetLanguageCode_Should_Return_Code(string locale)
    {
        string expected = locale.Contains('-') ? locale.Split('-')[0] : locale;

        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns(locale);

        string language = interactionMock.Object.GetLanguageCode();
        Assert.Equal(expected, language);
    }

    [Theory]
    [InlineData("", "en")]
    [InlineData(null, "es")]
    public void Interaction_GetLanguageCode_Should_Return_Default(string? locale, string defaultLanguage)
    {
        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns(locale!);
        var interactionMock2 = new Mock<IDiscordInteraction>();
        interactionMock2.SetupGet(x => x.UserLocale).Returns((string)null!);
        interactionMock2.SetupGet(x => x.GuildLocale).Returns(locale!);

        string language = interactionMock.Object.GetLanguageCode(defaultLanguage);
        string language2 = interactionMock2.Object.GetLanguageCode(defaultLanguage);

        interactionMock.VerifyGet(x => x.UserLocale, Times.Once);
        interactionMock2.VerifyGet(x => x.UserLocale, Times.Once);
        interactionMock2.VerifyGet(x => x.GuildLocale, Times.Once);
        Assert.Equal(defaultLanguage, language);
        Assert.Equal(defaultLanguage, language2);
    }

    [Theory]
    [MemberData(nameof(GetLanguages))]
    public void Interaction_TryGetLanguage_Should_Return_True_If_Valid(string language)
    {
        var interactionMock = new Mock<IDiscordInteraction>();
        if (Random.Shared.Next(2) == 0)
        {
            interactionMock.SetupGet(x => x.UserLocale).Returns(language);
        }
        else
        {
            interactionMock.SetupGet(x => x.GuildLocale).Returns(language);
        }

        bool success = interactionMock.Object.TryGetLanguage(out _);

        Assert.True(success);
    }

    public static TheoryData<string> GetLanguages()
    {
        var faker = new Faker();

        return Language.LanguageDictionary.Values
            .Select(x => x.ISO6391.Contains('-') ? x.ISO6391 : $"{x.ISO6391}-{faker.Random.String2(2)}")
            .Append(null!)
            .ToTheoryData();
    }

    public static TheoryData<string> GetLocales()
    {
        var faker = new Faker();

        return faker.MakeLazy(10, () => faker.Random.RandomLocale().Replace('_', '-'))
            .ToTheoryData();
    }

    public static TheoryData<string> GetRandomStrings()
    {
        var faker = new Faker();

        return faker.MakeLazy(10, () => faker.Random.String2(2))
            .ToTheoryData();
    }
}