using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Fergun.Extensions;
using GTranslate;
using Moq;
using Xunit;

namespace Fergun.Tests;

public class ExtensionsTests
{
    // Channel extensions
    [Fact]
    public void IMessageChannel_IsNsfw_Should_Return_True_When_Channel_Is_NSFW()
    {
        var channelMock1 = new Mock<ITextChannel>();
        channelMock1.SetupGet(x => x.IsNsfw).Returns(true);

        var channelMock2 = new Mock<ITextChannel>();
        channelMock2.SetupGet(x => x.IsNsfw).Returns(false);

        var channelMock3 = new Mock<IDMChannel>();

        Assert.True(channelMock1.Object.IsNsfw());
        Assert.False(channelMock2.Object.IsNsfw());
        Assert.False(channelMock3.Object.IsNsfw());
    }

    [Fact]
    public void IChannel_IsPrivate_Should_Return_True_When_Channel_Is_IPrivateChannel()
    {
        var channelMock1 = new Mock<IChannel>();
        var channelMock2 = new Mock<ITextChannel>();
        var channelMock3 = new Mock<IDMChannel>();

        Assert.False(channelMock1.Object.IsPrivate());
        Assert.False(channelMock2.Object.IsPrivate());
        Assert.True(channelMock3.Object.IsPrivate());
    }

    // Interaction extensions
    [Fact]
    public async Task Interaction_RespondWarningAsync_Should_Call_RespondAsync_Once()
    {
        var interactionMock = new Mock<IDiscordInteraction>();
        await interactionMock.Object.RespondWarningAsync(It.IsAny<string>(), It.IsAny<bool>());

        interactionMock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

    [Fact]
    public async Task Interaction_FollowupWarningAsync_Should_Call_FollowupAsync_Once()
    {
        var interactionMock = new Mock<IDiscordInteraction>();
        await interactionMock.Object.FollowupWarning(It.IsAny<string>(), It.IsAny<bool>());

        interactionMock.Verify(x => x.FollowupAsync(It.IsAny<string>(), It.IsAny<Embed[]>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()), Times.Once);
    }

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
    public void Interaction_GetLanguageCode_Should_Return_Default(string locale, string defaultLanguage)
    {
        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns(locale);
        var interactionMock2 = new Mock<IDiscordInteraction>();
        interactionMock2.SetupGet(x => x.UserLocale).Returns((string)null!);
        interactionMock2.SetupGet(x => x.GuildLocale).Returns(locale);

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

    // Message extensions
    [Theory]
    [MemberData(nameof(GetRandomStrings))]
    public void IMessage_GetText_Should_Return_Text_From_Content(string content)
    {
        var messageMock = new Mock<IMessage>();
        messageMock.SetupGet(x => x.Content).Returns(content);
        messageMock.SetupGet(x => x.Embeds).Returns(Array.Empty<IEmbed>());

        string text = messageMock.Object.GetText();

        messageMock.VerifyGet(x => x.Content);
        Assert.Equal(content, text);
    }

    [Theory]
    [MemberData(nameof(GetContentsAndEmbeds))]
    public void IMessage_GetText_Should_Return_Text_From_Content_And_Embed(string content, Embed embed)
    {
        var messageMock = new Mock<IMessage>();
        messageMock.SetupGet(x => x.Content).Returns(content);
        messageMock.SetupGet(x => x.Embeds).Returns(new[] { embed });

        string[] parts = messageMock.Object.GetText().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        messageMock.VerifyGet(x => x.Content);
        messageMock.VerifyGet(x => x.Embeds);

        int index = 0;
        Assert.True(parts.Length >= 3);
        Assert.Equal(content, parts[index++]);
        if (embed.Author is not null)
        {
            Assert.Equal(embed.Author.Value.Name, parts[index++]);
        }
        Assert.Equal(embed.Title, parts[index++]);
        Assert.Equal(embed.Description, parts[index]);
        if (embed.Footer is not null)
        {
            Assert.Equal(embed.Footer.Value.Text, parts[^1]);
        }
    }

    // String extensions
    [Theory]
    [InlineData("one two three", "one", "three")]
    [InlineData("1234", "123", "456")]
    [InlineData("1234", "012", "234")]
    [InlineData("abcde", "efg", "hij")]
    public void String_ContainsAny_Should_Return_Expected(string str, string str0, string str1)
    {
        bool containsFirst = str.Contains(str0);
        bool containsSecond = str.Contains(str1);

        bool containsAny = str.ContainsAny(str0, str1);

        Assert.Equal(containsAny, containsFirst || containsSecond);
    }

    // Timestamp extensions
    [Theory]
    [MemberData(nameof(GetDatesAndStyles))]
    public void DateTimeOffset_ToDiscordTimestamp_Should_Return_Expected(DateTimeOffset dateTimeOffset, char style)
    {
        var unixSeconds = dateTimeOffset.ToUnixTimeSeconds();

        string timestamp = dateTimeOffset.ToDiscordTimestamp(style);

        Assert.Equal(timestamp, $"<t:{unixSeconds}:{style}>");
    }

    private static IEnumerable<object[]> GetLocales()
    {
        var faker = new Faker();

        return faker.MakeLazy(10, () => faker.Random.RandomLocale().Replace('_', '-'))
            .Select(x => new object[] { x });
    }

    private static IEnumerable<object[]> GetRandomStrings()
    {
        var faker = new Faker();

        return faker.MakeLazy(10, () => faker.Random.String2(2))
            .Select(x => new object[] { x });
    }

    private static IEnumerable<object?[]> GetLanguages()
    {
        var faker = new Faker();

        return Language.LanguageDictionary.Values
            .Select(x => x.ISO6391.Contains('-') ? x.ISO6391 : $"{x.ISO6391}-{faker.Random.String2(2)}")
            .Append(null)
            .Select(x => new object?[] { x });
    }

    private static IEnumerable<object[]> GetEmbeds()
    {
        var embedFaker = new Faker<EmbedBuilder>()
            .StrictMode(false)
            .RuleFor(x => x.Author, f => new EmbedAuthorBuilder
            {
                IconUrl = f.Internet.Avatar(),
                Name = f.Internet.UserName(),
                Url = f.Internet.Url()
            }.OrNull(f))
            .RuleFor(x => x.Title, f => f.Lorem.Sentence())
            .RuleFor(x => x.Description, f => f.Lorem.Paragraph())
            .RuleFor(x => x.Fields, f =>
            {
                return f.MakeLazy(f.Random.Number(1, 10), () => new EmbedFieldBuilder
                {
                    IsInline = f.Random.Bool(),
                    Name = f.Random.Word(),
                    Value = f.Random.Word()
                }).Append(new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b" }).ToList();
            })
            .RuleFor(x => x.Footer, f => new EmbedFooterBuilder
            {
                IconUrl = f.Image.PlaceImgUrl(),
                Text = f.Commerce.ProductName()
            }.OrNull(f))
            .RuleFor(x => x.Color, f => new Color(f.Random.UInt(0, 0xFFFFFFU)));

        return embedFaker.GenerateLazy(10).Select(x => new object[] { x.Build() });
    }

    private static IEnumerable<object[]> GetContentsAndEmbeds()
    {
        return GetRandomStrings().Zip(GetEmbeds()).Select(x => new[] { x.First[0], x.Second[0] });
    }

    private static IEnumerable<object[]> GetDatesAndStyles()
    {
        var faker = new Faker();
        return faker.MakeLazy(10, () => (faker.Date.BetweenOffset(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), faker.Random.Char()))
            .Select(x => new object[] { x.Item1, x.Item2 });
    }
}