using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bogus;
using Discord;
using Discord.Interactions;
using Fergun.Apis.Dictionary;
using Fergun.Apis.Wikipedia;
using Fergun.Apis.WolframAlpha;
using Fergun.Common;
using Fergun.Interactive;
using Fergun.Localization;
using Fergun.Modules;
using Fergun.Services;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YoutubeExplode.Search;

namespace Fergun.Tests.Modules;

public class UtilityModuleTests : ModuleTestBase<UtilityModule>
{
    private readonly GoogleTranslator2 _googleTranslator2 = new();
    private readonly SearchClient _searchClient = new(new HttpClient());
    private readonly IWikipediaClient _wikipediaClient = null!;
    private readonly IWolframAlphaClient _wolframAlphaClient = null!;
    private readonly IDictionaryClient _dictionaryClient = null!;

    public UtilityModuleTests()
    {
        var commandCache = new ApplicationCommandCache();
        var options = Utils.CreateMockedFergunOptions();
        var shared = new SharedModule(Mock.Of<ILogger<SharedModule>>(), Utils.CreateMockedLocalizer<SharedResource>(), Mock.Of<IFergunTranslator>(), _googleTranslator2);
        var interactionService = new InteractionService(Client);
        var interactive = new InteractiveService(Client, new InteractiveConfig { ReturnAfterSendingPaginator = true });

        SetupModule(new Mock<UtilityModule>(() => new UtilityModule(Logger, Localizer, Emotes, interactive, options, shared,
            interactionService, commandCache, _dictionaryClient, Mock.Of<IFergunTranslator>(), _searchClient, _wikipediaClient, _wolframAlphaClient))
        {
            CallBase = true
        });
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers), DisableDiscoveryEnumeration = true)]
    public async Task AvatarAsync_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        var result = await Module.AvatarAsync(userMock.Object);
        Assert.True(result.IsSuccess);

        userMock.Verify(x => x.ToString());
        userMock.Verify(x => x.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        if (userMock.Object.GetAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()) is null)
        {
            userMock.Verify(x => x.GetDefaultAvatarUrl());
        }

        VerifyAvatarEmbed(userMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeGuildUsers), DisableDiscoveryEnumeration = true)]
    public async Task AvatarAsync_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        var result = await Module.AvatarAsync(guildUserMock.Object);
        Assert.True(result.IsSuccess);

        guildUserMock.Verify(x => x.ToString());
        guildUserMock.Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        VerifyAvatarEmbed(guildUserMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeUsers), DisableDiscoveryEnumeration = true)]
    public async Task UserInfoAsync_Should_Return_Embed_With_Avatar(Mock<IUser> userMock)
    {
        var result = await Module.UserInfoAsync(userMock.Object);
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

        VerifyAvatarEmbed(userMock.Object);
    }

    [Theory]
    [MemberData(nameof(GetFakeGuildUsers), DisableDiscoveryEnumeration = true)]
    public async Task UserInfoAsync_Should_Return_Embed_With_Guild_Avatar(Mock<IGuildUser> guildUserMock)
    {
        var result = await Module.UserInfoAsync(guildUserMock.Object);
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

        VerifyAvatarEmbed(guildUserMock.Object);
    }

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

    private void VerifyAvatarEmbed(IUser user)
        => InteractionMock.VerifyRespondAsync(e => EmbedImageUrlIsUserAvatarUrl(user, e), Times.Once());

    private static bool EmbedImageUrlIsUserAvatarUrl(IUser user, Embed embed)
        => (embed.Image.GetValueOrDefault().Url ?? embed.Thumbnail.GetValueOrDefault().Url)
           == ((user as IGuildUser)?.GetGuildAvatarUrl() ?? user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());
}