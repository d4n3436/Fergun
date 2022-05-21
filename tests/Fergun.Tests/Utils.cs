using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AutoBogus;
using Bogus;
using Discord;
using Fergun.Apis.Bing;
using Fergun.Apis.Urban;
using Fergun.Apis.Yandex;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;

namespace Fergun.Tests;

internal static class Utils
{
    public static T CreateInstance<T>(params object?[]? args) where T : class
        => (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null, args, CultureInfo.InvariantCulture)!;

    public static IFergunLocalizer<T> CreateMockedLocalizer<T>()
    {
        var localizerMock = new Mock<IFergunLocalizer<T>>();
        localizerMock.Setup(x => x[It.IsAny<string>()]).Returns<string>(s => new LocalizedString(s, s));
        localizerMock.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((s, p) => new LocalizedString(s, string.Format(s, p)));
        localizerMock.SetupAllProperties();

        return localizerMock.Object;
    }

    public static IUser CreateMockedUser(Faker? faker = null)
    {
        faker ??= new Faker();
        var userMock = new Mock<IUser>();

        userMock.SetupGet(x => x.Username).Returns(faker.Internet.UserName());
        userMock.SetupGet(x => x.DiscriminatorValue).Returns(faker.Random.UShort(1, 9999));
        userMock.SetupGet(x => x.Discriminator).Returns(() => userMock.Object.DiscriminatorValue.ToString("D4"));
        userMock.SetupGet(x => x.Activities).Returns(faker.MakeLazy(faker.Random.Number(3), () => new Game(faker.Hacker.IngVerb(), faker.Random.Enum(ActivityType.CustomStatus))
            .OrDefault(faker, 0.5f, CreateFakeCustomStatusGame(faker))).ToArray());
        userMock.SetupGet(x => x.ActiveClients).Returns(faker.MakeLazy(faker.Random.Number(3),
            () => faker.PickRandom(Enum.GetValues<ClientType>()).OrDefault(faker, 0.5f, (ClientType)3)).ToArray());
        userMock.SetupGet(x => x.CreatedAt).Returns(faker.Date.PastOffset(5));
        userMock.SetupGet(x => x.Id).Returns(faker.Random.ULong());
        userMock.SetupGet(x => x.IsBot).Returns(faker.Random.Bool());
        userMock.Setup(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns(faker.Internet.Avatar().OrNull(faker));
        userMock.Setup(x => x.GetDefaultAvatarUrl()).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.ToString()).Returns(() => $"{userMock.Object.Username}#{userMock.Object.Discriminator}");

        return userMock.Object;
    }

    public static IGuildUser CreateMockedGuildUser(Faker? faker = null)
    {
        faker ??= new Faker();
        var userMock = new Mock<IGuildUser>();

        userMock.SetupGet(x => x.Username).Returns(faker.Internet.UserName());
        userMock.SetupGet(x => x.DiscriminatorValue).Returns(faker.Random.UShort(1, 9999));
        userMock.SetupGet(x => x.Discriminator).Returns(userMock.Object.DiscriminatorValue.ToString("D4"));
        userMock.SetupGet(x => x.Activities).Returns(faker.MakeLazy(faker.Random.Number(3), () => new Game(faker.Hacker.IngVerb(), faker.Random.Enum(ActivityType.CustomStatus))
            .OrDefault(faker, 0.5f, CreateFakeCustomStatusGame(faker))).ToArray());
        userMock.SetupGet(x => x.ActiveClients).Returns(faker.MakeLazy(faker.Random.Number(3),
            () => faker.PickRandom(Enum.GetValues<ClientType>()).OrDefault(faker, 0.5f, (ClientType)3)).ToArray());
        userMock.SetupGet(x => x.CreatedAt).Returns(faker.Date.PastOffset(5));
        userMock.SetupGet(x => x.Id).Returns(faker.Random.ULong());
        userMock.SetupGet(x => x.IsBot).Returns(faker.Random.Bool());
        userMock.SetupGet(x => x.Nickname).Returns(faker.Internet.UserName().OrNull(faker));
        userMock.SetupGet(x => x.JoinedAt).Returns( faker.Date.PastOffset());
        userMock.SetupGet(x => x.PremiumSince).Returns(faker.Date.PastOffset().OrNull(faker));
        userMock.Setup(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.GetDefaultAvatarUrl()).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.ToString()).Returns($"{userMock.Object.Username}#{userMock.Object.Discriminator}");

        return userMock.Object;
    }

    public static CustomStatusGame CreateFakeCustomStatusGame(Faker? faker = null)
    {
        faker ??= new Faker();

        var status = CreateInstance<CustomStatusGame>();
        status.SetPropertyValue(x => x.Emote, Emote.Parse($"<:{faker.Random.String2(10)}:{faker.Random.ULong()}>"));
        status.SetPropertyValue(x => x.State, faker.Hacker.IngVerb());
        status.SetPropertyValue(x => x.Type, ActivityType.CustomStatus);
        return status;
    }

    public static EmbedBuilder CreateFakeEmbedBuilder()
    {
        return new Faker<EmbedBuilder>()
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
            .RuleFor(x => x.Color, f => new Color(f.Random.UInt(0, 0xFFFFFFU)))
            .Generate();
    }

    public static IBingVisualSearch CreateMockedBingVisualSearchApi(Faker? faker = null)
    {
        faker ??= new Faker();
        var bingMock = new Mock<IBingVisualSearch>();

        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == string.Empty))).ReturnsAsync(() => string.Empty);
        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)))).ReturnsAsync(() => faker.Lorem.Sentence());
        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == "https://example.com/error"))).ThrowsAsync(new BingException("Error message."));

        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == string.Empty), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ReturnsAsync(Enumerable.Empty<IBingReverseImageSearchResult>);
        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ReturnsAsync(() => faker.MakeLazy(50, () => CreateMockedBingReverseImageSearchResult(faker)));
        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == "https://example.com/error"), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ThrowsAsync(new BingException("Error message."));

        return bingMock.Object;
    }

    public static IBingReverseImageSearchResult CreateMockedBingReverseImageSearchResult(Faker? faker = null)
    {
        faker ??= new Faker();
        var resultMock = new Mock<IBingReverseImageSearchResult>();

        resultMock.SetupGet(x => x.Url).Returns(faker.Internet.Url());
        resultMock.SetupGet(x => x.FriendlyDomainName).Returns(faker.Internet.DomainName().OrNull(faker));
        resultMock.SetupGet(x => x.SourceUrl).Returns(faker.Internet.Url());
        resultMock.SetupGet(x => x.Text).Returns(faker.Commerce.ProductDescription());
        resultMock.SetupGet(x => x.AccentColor).Returns(System.Drawing.Color.FromArgb(faker.Random.Number((int)Color.MaxDecimalValue)));

        return resultMock.Object;
    }

    public static IYandexImageSearch CreateMockedYandexImageSearchApi(Faker? faker = null)
    {
        faker ??= new Faker();
        var yandexMock = new Mock<IYandexImageSearch>();

        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == string.Empty))).ReturnsAsync(() => string.Empty);
        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)))).ReturnsAsync(() => faker.Lorem.Sentence());
        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == "https://example.com/error"))).ThrowsAsync(new YandexException("Error message."));
        
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == string.Empty), It.IsAny<YandexSearchFilterMode>())).ReturnsAsync(Enumerable.Empty<IYandexReverseImageSearchResult>);
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<YandexSearchFilterMode>())).ReturnsAsync(() => faker.MakeLazy(50, () => CreateMockedYandexReverseImageSearchResult(faker)));
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == "https://example.com/error"), It.IsAny<YandexSearchFilterMode>())).ThrowsAsync(new YandexException("Error message."));

        return yandexMock.Object;
    }

    public static IYandexReverseImageSearchResult CreateMockedYandexReverseImageSearchResult(Faker? faker = null)
    {
        faker ??= new Faker();
        var resultMock = new Mock<IYandexReverseImageSearchResult>();

        resultMock.SetupGet(x => x.Url).Returns(faker.Internet.Url());
        resultMock.SetupGet(x => x.SourceUrl).Returns(faker.Internet.Url());
        resultMock.SetupGet(x => x.Title).Returns(faker.Commerce.ProductName());
        resultMock.SetupGet(x => x.Text).Returns(faker.Commerce.ProductDescription());

        return resultMock.Object;
    }

    public static IUrbanDictionary CreateMockedUrbanDictionaryApi(Faker? faker = null)
    {
        faker ??= new Faker();
        var mock = new Mock<IUrbanDictionary>();

        mock.Setup(u => u.GetDefinitionsAsync(It.IsNotNull<string>())).ReturnsAsync(() => faker.MakeLazy(10, CreateFakeUrbanDefinition).ToList());
        mock.Setup(u => u.GetDefinitionsAsync(It.Is<string>(s => s == null))).ReturnsAsync(Array.Empty<UrbanDefinition>());
        mock.Setup(u => u.GetRandomDefinitionsAsync()).ReturnsAsync(() => faker.MakeLazy(10, CreateFakeUrbanDefinition).ToList());
        mock.Setup(u => u.GetDefinitionAsync(It.IsAny<int>())).ReturnsAsync(CreateFakeUrbanDefinition);
        mock.Setup(u => u.GetWordsOfTheDayAsync()).ReturnsAsync(() => faker.MakeLazy(10, CreateFakeUrbanDefinition).ToList());
        mock.Setup(u => u.GetAutocompleteResultsAsync(It.IsAny<string>())).ReturnsAsync(AutoFaker.Generate<string>(20));
        mock.Setup(u => u.GetAutocompleteResultsExtraAsync(It.IsAny<string>())).ReturnsAsync(AutoFaker.Generate<UrbanAutocompleteResult>(20));

        return mock.Object;
    }

    public static UrbanDefinition CreateFakeUrbanDefinition()
    {
        return new AutoFaker<UrbanDefinition>()
            .RuleFor(x => x.Definition, f => f.Lorem.Sentence())
            .RuleFor(x => x.Date, f => f.Date.Weekday().OrNull(f))
            .RuleFor(x => x.Permalink, f => f.Internet.Url())
            .RuleFor(x => x.ThumbsUp, f => f.Random.Int())
            .RuleFor(x => x.SoundUrls, Array.Empty<string>())
            .RuleFor(x => x.Author, f => f.Internet.UserName())
            .RuleFor(x => x.Word, f => f.Lorem.Word())
            .RuleFor(x => x.Id, f => f.Random.Int())
            .RuleFor(x => x.WrittenOn, f => f.Date.PastOffset())
            .RuleFor(x => x.Example, f => f.Lorem.Sentence())
            .Generate();
    }

    public static IOptionsSnapshot<FergunOptions> CreateMockedFergunOptions()
    {
        var mock = new Mock<IOptionsSnapshot<FergunOptions>>();
        var faker = new Faker<FergunOptions>()
            .RuleFor(x => x.PaginatorTimeout, f => f.Date.Timespan())
            .RuleFor(x => x.SelectionTimeout, f => f.Date.Timespan())
            .RuleFor(x => x.PaginatorEmotes, f => new Dictionary<PaginatorAction, string>
            {
                { PaginatorAction.Backward, "◀️" }, // Valid emoji
                { PaginatorAction.Forward, $"<:forward:{f.Random.ULong()}>" }, // Valid emote
                { PaginatorAction.Jump, "123" }, // Invalid emote
                { PaginatorAction.Exit, "🛑" }
            });

        mock.Setup(x => x.Value).Returns(() => faker.Generate());

        return mock.Object;
    }
}