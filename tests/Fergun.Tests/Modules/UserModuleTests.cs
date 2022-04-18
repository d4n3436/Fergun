using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Fergun.Modules;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class UserModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly IFergunLocalizer<UserModule> _localizer = Utils.CreateMockedLocalizer<UserModule>();
    private readonly Mock<UserModule> _moduleMock;

    public UserModuleTests()
    {
        _moduleMock = new Mock<UserModule>(() => new UserModule(_localizer)) { CallBase = true };
        _contextMock.SetupGet(x => x.Interaction).Returns(_interactionMock.Object);
        ((IInteractionModuleBase)_moduleMock.Object).SetContext(_contextMock.Object);
    }

    [Fact]
    public void BeforeExecute_Sets_Language()
    {
        _interactionMock.SetupGet(x => x.UserLocale).Returns("en");
        _moduleMock.Object.BeforeExecute(It.IsAny<ICommandInfo>());
        Assert.Equal("en", _localizer.CurrentCulture.TwoLetterISOLanguageName);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers))]
    public async Task Avatar_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        await _moduleMock.Object.Avatar(userMock.Object);

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
        await _moduleMock.Object.Avatar(guildUserMock.Object);

        guildUserMock.Verify(x => x.ToString());
        guildUserMock.Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        VerifyRespondAsyncCall(guildUserMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers))]
    public async Task UserInfo_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        await _moduleMock.Object.UserInfo(userMock.Object);

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
        await _moduleMock.Object.UserInfo(guildUserMock.Object);

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
        _interactionMock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(),
            It.Is<Embed>(e => EmbedImageUrlIsUserAvatarUrl(user, e)), It.IsAny<RequestOptions>()), Times.Once);
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