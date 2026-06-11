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
    [MemberData(nameof(GetUserSeeds))]
    public async Task AvatarAsync_Returns_Embed_With_FirstAvailable_Avatar(int seed)
    {
        var user = Utils.CreateMockedUser(new Faker { Random = new Randomizer(seed) });

        var result = await Module.AvatarAsync(user);
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyRespondAsync(e => e.Title == user.ToString(), Times.Once());
        InteractionMock.VerifyRespondAsync(e => e.Image.GetValueOrDefault().Url == ExpectedAvatarUrl(user), Times.Once());
    }

    [Theory]
    [MemberData(nameof(GetUserSeeds))]
    public async Task AvatarAsync_Returns_Embed_With_Guild_Avatar(int seed)
    {
        var guildUser = Utils.CreateMockedGuildUser(new Faker { Random = new Randomizer(seed) });

        var result = await Module.AvatarAsync(guildUser);
        Assert.True(result.IsSuccess);

        string? expectedUrl = guildUser.GetGuildAvatarUrl(size: 2048);
        Mock.Get(guildUser).Verify(x => x.GetGuildAvatarUrl(It.IsAny<ImageFormat>(), It.IsAny<ushort>()));
        InteractionMock.VerifyRespondAsync(e => e.Image.GetValueOrDefault().Url == expectedUrl, Times.Once());
    }

    [Fact]
    public async Task AvatarAsync_Uses_Custom_Avatar_When_Present()
    {
        var user = Utils.CreateMockedUser(hasAvatar: true);

        var result = await Module.AvatarAsync(user);
        Assert.True(result.IsSuccess);

        string? expectedUrl = user.GetAvatarUrl(size: 2048);
        InteractionMock.VerifyRespondAsync(e => e.Image.GetValueOrDefault().Url == expectedUrl, Times.Once());
    }

    [Fact]
    public async Task AvatarAsync_Falls_Back_To_Default_Avatar_When_Absent()
    {
        var user = Utils.CreateMockedUser(hasAvatar: false);

        var result = await Module.AvatarAsync(user);
        Assert.True(result.IsSuccess);

        Mock.Get(user).Verify(x => x.GetDefaultAvatarUrl());
        InteractionMock.VerifyRespondAsync(e => e.Image.GetValueOrDefault().Url == user.GetDefaultAvatarUrl(), Times.Once());
    }

    [Fact]
    public async Task AvatarAsync_Server_Returns_Error_When_User_Has_No_Guild_Avatar()
    {
        var user = Utils.CreateMockedUser(); // plain IUser: no guild avatar

        var result = await Module.AvatarAsync(user, AvatarType.Server);

        Assert.False(result.IsSuccess);
        InteractionMock.Verify(x => x.RespondAsync(It.IsAny<string>(), It.IsAny<Embed[]>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(), It.IsAny<MessageComponent>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>(), It.IsAny<PollProperties>(), It.IsAny<MessageFlags>()), Times.Never);
    }

    [Fact]
    public async Task AvatarAsync_Global_Returns_Error_When_User_Has_No_Avatar()
    {
        var user = Utils.CreateMockedUser(hasAvatar: false);

        var result = await Module.AvatarAsync(user, AvatarType.Global);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [MemberData(nameof(GetUserSeeds))]
    public async Task UserInfoAsync_Returns_Embed_With_User_Fields(int seed)
    {
        var user = Utils.CreateMockedUser(new Faker { Random = new Randomizer(seed) });

        var result = await Module.UserInfoAsync(user);
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "Name" && f.Value == user.ToString()), Times.Once());
        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "ID" && f.Value == user.Id.ToString()), Times.Once());
        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "IsBot" && f.Value == (user.IsBot ? "Yes" : "No")), Times.Once());
        InteractionMock.VerifyRespondAsync(e => e.Thumbnail.GetValueOrDefault().Url == ExpectedAvatarUrl(user), Times.Once());
    }

    [Theory]
    [MemberData(nameof(GetUserSeeds))]
    public async Task UserInfoAsync_Returns_Embed_With_Guild_Fields(int seed)
    {
        var guildUser = Utils.CreateMockedGuildUser(new Faker { Random = new Randomizer(seed) });

        var result = await Module.UserInfoAsync(guildUser);
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "Name" && f.Value == guildUser.ToString()), Times.Once());
        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "Nickname" && f.Value == (guildUser.Nickname ?? "(None)")), Times.Once());
        string? expectedUrl = guildUser.GetGuildAvatarUrl(size: 2048);
        InteractionMock.VerifyRespondAsync(e => e.Thumbnail.GetValueOrDefault().Url == expectedUrl, Times.Once());
    }

    [Fact]
    public async Task UserInfoAsync_Renders_None_For_User_Without_Activities()
    {
        var user = Utils.CreateMockedUser(activityCount: 0);

        var result = await Module.UserInfoAsync(user);
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "Activities" && f.Value == "(None)"), Times.Once());
    }

    [Theory]
    [InlineData(true)] // pomelo username (no discriminator)
    [InlineData(false)] // legacy username#0000
    public async Task UserInfoAsync_Name_Field_Uses_ToString_Format(bool pomelo)
    {
        var user = Utils.CreateMockedUser(pomelo: pomelo);

        var result = await Module.UserInfoAsync(user);
        Assert.True(result.IsSuccess);

        string expectedName = pomelo ? user.Username : $"{user.Username}#{user.Discriminator}";
        Assert.Equal(expectedName, user.ToString());
        InteractionMock.VerifyRespondAsync(e => e.Fields.Any(f => f.Name == "Name" && f.Value == expectedName), Times.Once());
    }

    public static TheoryData<int> GetUserSeeds() => Enumerable.Range(1, 20).ToTheoryData();

    private static string ExpectedAvatarUrl(IUser user)
        => (user as IGuildUser)?.GetGuildAvatarUrl(size: 2048) ?? user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl();
}
