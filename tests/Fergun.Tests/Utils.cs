using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bogus;
using Discord;
using Fergun.Apis.Bing;
using Fergun.Apis.Yandex;
using Microsoft.Extensions.Localization;
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
        userMock.SetupGet(x => x.Activities).Returns(() => faker.MakeLazy(faker.Random.Number(3), () => new Game(faker.Hacker.IngVerb(), faker.Random.Enum(ActivityType.CustomStatus))
            .OrDefault(faker, 0.5f, CreateCustomStatusGame(faker))).ToArray());
        userMock.SetupGet(x => x.ActiveClients).Returns(() => faker.MakeLazy(faker.Random.Number(3),
            () => faker.PickRandom(Enum.GetValues<ClientType>()).OrDefault(faker, 0.5f, (ClientType)3)).ToArray());
        userMock.SetupGet(x => x.CreatedAt).Returns(() => faker.Date.PastOffset(5));
        userMock.SetupGet(x => x.Id).Returns(() => faker.Random.ULong());
        userMock.SetupGet(x => x.IsBot).Returns(() => faker.Random.Bool());
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
        userMock.SetupGet(x => x.Discriminator).Returns(() => userMock.Object.DiscriminatorValue.ToString("D4"));
        userMock.SetupGet(x => x.Activities).Returns(() => faker.MakeLazy(faker.Random.Number(3), () => new Game(faker.Hacker.IngVerb(), faker.Random.Enum(ActivityType.CustomStatus))
            .OrDefault(faker, 0.5f, CreateCustomStatusGame(faker))).ToArray());
        userMock.SetupGet(x => x.ActiveClients).Returns(() => faker.MakeLazy(faker.Random.Number(3),
            () => faker.PickRandom(Enum.GetValues<ClientType>()).OrDefault(faker, 0.5f, (ClientType)3)).ToArray());
        userMock.SetupGet(x => x.CreatedAt).Returns(() => faker.Date.PastOffset(5));
        userMock.SetupGet(x => x.Id).Returns(() => faker.Random.ULong());
        userMock.SetupGet(x => x.IsBot).Returns(() => faker.Random.Bool());
        userMock.SetupGet(x => x.Nickname).Returns(() => faker.Internet.UserName().OrNull(faker));
        userMock.SetupGet(x => x.JoinedAt).Returns(() => faker.Date.PastOffset());
        userMock.SetupGet(x => x.PremiumSince).Returns(() => faker.Date.PastOffset().OrNull(faker));
        userMock.Setup(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>())).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.GetDefaultAvatarUrl()).Returns(faker.Internet.Avatar());
        userMock.Setup(x => x.ToString()).Returns(() => $"{userMock.Object.Username}#{userMock.Object.Discriminator}");

        return userMock.Object;
    }

    public static CustomStatusGame CreateCustomStatusGame(Faker? faker = null)
    {
        faker ??= new Faker();

        var status = CreateInstance<CustomStatusGame>();
        status.SetPropertyValue(x => x.Emote, Emote.Parse($"<:{faker.Random.String2(10)}:{faker.Random.ULong()}>"));
        status.SetPropertyValue(x => x.State, faker.Hacker.IngVerb());
        status.SetPropertyValue(x => x.Type, ActivityType.CustomStatus);
        return status;
    }

    public static IBingVisualSearch CreateMockedBingVisualSearchApi(Faker? faker = null)
    {
        var bingMock = new Mock<IBingVisualSearch>();
        faker ??= new Faker();

        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == null))).ThrowsAsync(new BingException("Error message."));
        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == string.Empty))).ReturnsAsync(() => string.Empty);
        bingMock.Setup(x => x.OcrAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)))).ReturnsAsync(() => faker.Lorem.Sentence());
        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == null), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ThrowsAsync(new BingException("Error message."));
        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == string.Empty), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ReturnsAsync(Enumerable.Empty<IBingReverseImageSearchResult>);
        bingMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<BingSafeSearchLevel>(), It.IsAny<string>())).ReturnsAsync(() => faker.MakeLazy(50, () => CreateMockedBingReverseImageSearchResult(faker)));

        return bingMock.Object;
    }

    public static IBingReverseImageSearchResult CreateMockedBingReverseImageSearchResult(Faker? faker = null)
    {
        var resultMock = new Mock<IBingReverseImageSearchResult>();
        faker = new Faker();

        resultMock.SetupGet(x => x.Url).Returns(() => faker.Internet.Url());
        resultMock.SetupGet(x => x.FriendlyDomainName).Returns(() => faker.Internet.DomainName().OrNull(faker));
        resultMock.SetupGet(x => x.SourceUrl).Returns(() => faker.Internet.Url());
        resultMock.SetupGet(x => x.Text).Returns(() => faker.Commerce.ProductDescription());
        resultMock.SetupGet(x => x.AccentColor).Returns(() => System.Drawing.Color.FromArgb(faker.Random.Number((int)Color.MaxDecimalValue)));

        return resultMock.Object;
    }

    public static IYandexImageSearch CreateMockedYandexImageSearchApi(Faker? faker = null)
    {
        var yandexMock = new Mock<IYandexImageSearch>();
        faker ??= new Faker();

        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == null))).ThrowsAsync(new YandexException("Error message."));
        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => s == string.Empty))).ReturnsAsync(() => string.Empty);
        yandexMock.Setup(x => x.OcrAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)))).ReturnsAsync(() => faker.Lorem.Sentence());
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == null), It.IsAny<YandexSearchFilterMode>())).ThrowsAsync(new YandexException("Error message."));
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => s == string.Empty), It.IsAny<YandexSearchFilterMode>())).ReturnsAsync(Enumerable.Empty<IYandexReverseImageSearchResult>);
        yandexMock.Setup(x => x.ReverseImageSearchAsync(It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<YandexSearchFilterMode>())).ReturnsAsync(() => faker.MakeLazy(50, () => CreateMockedYandexReverseImageSearchResult(faker)));

        return yandexMock.Object;
    }

    public static IYandexReverseImageSearchResult CreateMockedYandexReverseImageSearchResult(Faker? faker = null)
    {
        var resultMock = new Mock<IYandexReverseImageSearchResult>();
        faker = new Faker();

        resultMock.SetupGet(x => x.Url).Returns(() => faker.Internet.Url());
        resultMock.SetupGet(x => x.SourceUrl).Returns(() => faker.Internet.Url());
        resultMock.SetupGet(x => x.Title).Returns(() => faker.Commerce.ProductName());
        resultMock.SetupGet(x => x.Text).Returns(() => faker.Commerce.ProductDescription());

        return resultMock.Object;
    }
}