using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Fergun.Modules;
using Microsoft.Extensions.Localization;
using Moq;
using Moq.Protected;
using Xunit;

namespace Fergun.Tests;

public class UserModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly Mock<UserModule> _userModuleMock;

    public UserModuleTests()
    {
        var userLocalizer = new Mock<IFergunLocalizer<UserModule>>();
        userLocalizer.Setup(x => x[It.IsAny<string>()]).Returns<string>(s => new LocalizedString(s, s));
        userLocalizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((s, p) => new LocalizedString(s, string.Format(s, p)));

        _userModuleMock = new Mock<UserModule>(() => new UserModule(userLocalizer.Object));
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        ((IInteractionModuleBase)_userModuleMock.Object).SetContext(_contextMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers))]
    public async Task Avatar_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        await _userModuleMock.Object.Avatar(userMock.Object);

        userMock.Verify(x => x.ToString());
        userMock.Verify(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        if (userMock.Object.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()) is null)
        {
            userMock.Verify(x => x.GetDefaultAvatarUrl());
        }

        VerifyRespondAsyncCall(userMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeGuildUsers))]
    public async Task Avatar_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        await _userModuleMock.Object.Avatar(guildUserMock.Object);

        guildUserMock.Verify(x => x.ToString());
        guildUserMock.Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        VerifyRespondAsyncCall(guildUserMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers))]
    public async Task UserInfo_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        await _userModuleMock.Object.UserInfo(userMock.Object);

        userMock.Verify(x => x.ToString());
        userMock.Verify(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        if (userMock.Object.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()) is null)
        {
            userMock.Verify(x => x.GetDefaultAvatarUrl());
        }

        userMock.VerifyGet(x => x.Activities);
        userMock.VerifyGet(x => x.ActiveClients);
        userMock.VerifyGet(x => x.Id);
        userMock.VerifyGet(x => x.IsBot);
        userMock.VerifyGet(x => x.CreatedAt);

        VerifyRespondAsyncCall(userMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeGuildUsers))]
    public async Task UserInfo_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        await _userModuleMock.Object.UserInfo(guildUserMock.Object);

        guildUserMock.Verify(x => x.ToString());
        guildUserMock.Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        guildUserMock.VerifyGet(x => x.Activities);
        guildUserMock.VerifyGet(x => x.ActiveClients);
        guildUserMock.VerifyGet(x => x.Id);
        guildUserMock.VerifyGet(x => x.IsBot);
        guildUserMock.VerifyGet(x => x.CreatedAt);
        guildUserMock.VerifyGet(x => x.Nickname);
        guildUserMock.VerifyGet(x => x.JoinedAt);
        guildUserMock.VerifyGet(x => x.PremiumSince);

        VerifyRespondAsyncCall(guildUserMock.Object);
    }

    private void VerifyRespondAsyncCall(IUser user)
    {
        _userModuleMock.Protected().Verify<Task>("RespondAsync", Times.Once(), ItExpr.IsAny<string>(),
            ItExpr.IsAny<Embed[]>(), ItExpr.IsAny<bool>(), ItExpr.IsAny<bool>(), ItExpr.IsAny<AllowedMentions>(),
            ItExpr.IsAny<RequestOptions>(), ItExpr.IsAny<MessageComponent>(),
            ItExpr.Is<Embed>(e => EmbedImageUrlIsUserAvatarUrl(user, e)));
    }

    private static bool EmbedImageUrlIsUserAvatarUrl(IUser user, Embed embed)
        => (embed.Image.GetValueOrDefault().Url ?? embed.Thumbnail.GetValueOrDefault().Url)
           == ((user as IGuildUser)?.GetGuildAvatarUrl() ?? user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());

    private static IEnumerable<object[]> GetFakeUsers()
    {
        var faker = new Faker();

        return faker.MakeLazy(20, () =>
        {
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

            return userMock;
        }).Select(x => new object[] { x });
    }

    private static IEnumerable<object[]> GetFakeGuildUsers()
    {
        var faker = new Faker();

        return faker.MakeLazy(20, () =>
        {
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

            return userMock;
        }).Select(x => new object[] { x });
    }

    private static CustomStatusGame CreateCustomStatusGame(Faker faker)
    {
        var status = Utils.CreateInstance<CustomStatusGame>();
        status.SetPropertyValue(x => x.Emote, Emote.Parse($"<:{faker.Random.String2(10)}:{faker.Random.ULong()}>"));
        status.SetPropertyValue(x => x.State, faker.Hacker.IngVerb());
        status.SetPropertyValue(x => x.Type, ActivityType.CustomStatus);
        return status;
    }
}