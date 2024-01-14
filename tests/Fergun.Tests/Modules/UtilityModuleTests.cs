using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Wikipedia;
using Fergun.Apis.WolframAlpha;
using Fergun.Configuration;
using Fergun.Interactive;
using Fergun.Modules;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using YoutubeExplode.Search;

namespace Fergun.Tests.Modules;

public class UtilityModuleTests
{
    private readonly Mock<IInteractionContext> _contextMock = new();
    private readonly Mock<IDiscordInteraction> _interactionMock = new();
    private readonly IFergunLocalizer<UtilityModule> _localizer = Utils.CreateMockedLocalizer<UtilityModule>();
    private readonly GoogleTranslator2 _googleTranslator2 = new();
    private readonly SearchClient _searchClient = new(new HttpClient());
    private readonly IWikipediaClient _wikipediaClient = null!;
    private readonly IWolframAlphaClient _wolframAlphaClient = null!;
    private readonly IDictionaryClient _dictionaryClient = null!;
    private readonly Mock<UtilityModule> _moduleMock;
    
    public UtilityModuleTests()
    {
        var startupOptions = Mock.Of<IOptions<StartupOptions>>();
        var options = Utils.CreateMockedFergunOptions();
        var client = new DiscordSocketClient();
        SharedModule shared = new(Mock.Of<ILogger<SharedModule>>(), Utils.CreateMockedLocalizer<SharedResource>(), Mock.Of<IFergunTranslator>(), _googleTranslator2);
        var interactionService = new InteractionService(client);
        var interactive = new InteractiveService(client, new InteractiveConfig { ReturnAfterSendingPaginator = true });
        _moduleMock = new Mock<UtilityModule>(() => new UtilityModule(Mock.Of<ILogger<UtilityModule>>(), _localizer, startupOptions, options, shared, interactionService,
            interactive, _dictionaryClient, Mock.Of<IFergunTranslator>(), _searchClient, _wikipediaClient, _wolframAlphaClient)) { CallBase = true };
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
    public async Task AvatarAsync_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        var result = await _moduleMock.Object.AvatarAsync(userMock.Object);
        Assert.True(result.IsSuccess);

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
    public async Task AvatarAsync_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        var result = await _moduleMock.Object.AvatarAsync(guildUserMock.Object);
        Assert.True(result.IsSuccess);

        guildUserMock.Verify(x => x.ToString());
        guildUserMock.Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        VerifyRespondAsyncCall(guildUserMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers))]
    public async Task UserInfoAsync_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        var result = await _moduleMock.Object.UserInfoAsync(userMock.Object);
        Assert.True(result.IsSuccess);

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
    public async Task UserInfoAsync_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        var result = await _moduleMock.Object.UserInfoAsync(guildUserMock.Object);
        Assert.True(result.IsSuccess);

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

    internal void VerifyRespondAsyncCall(IUser user)
    {
        _interactionMock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(),
            It.Is<Embed>(e => EmbedImageUrlIsUserAvatarUrl(user, e)), It.IsAny<RequestOptions>()), Times.Once);
    }

    public static bool EmbedImageUrlIsUserAvatarUrl(IUser user, Embed embed)
        => (embed.Image.GetValueOrDefault().Url ?? embed.Thumbnail.GetValueOrDefault().Url)
           == ((user as IGuildUser)?.GetGuildAvatarUrl() ?? user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());

    public static TheoryData<Mock<IUser>> GetFakeUsers()
    {
        var faker = new Faker();

        return faker.MakeLazy(20, () => Utils.CreateMockedUser()).Select(Mock.Get).ToTheoryData();
    }

    public static TheoryData<Mock<IGuildUser>> GetFakeGuildUsers()
    {
        var faker = new Faker();

        return faker.MakeLazy(20, () => Utils.CreateMockedGuildUser()).Select(Mock.Get).ToTheoryData();
    }
}