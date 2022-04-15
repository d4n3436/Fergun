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

        return faker.MakeLazy(20, () => Utils.CreateMockedUser()).Select(x => new object[] { Mock.Get(x) });
    }

    private static IEnumerable<object[]> GetFakeGuildUsers()
    {
        var faker = new Faker();

        return faker.MakeLazy(20, () => Utils.CreateMockedGuildUser()).Select(x => new object[] { Mock.Get(x) });
    }
}