using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoBogus;
using AutoBogus.Moq;
using Discord;
using Fergun.Apis.Urban;
using Fergun.Common;
using Fergun.Interactive;
using Fergun.Modules;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class UrbanModuleTests : ModuleTestBase<UrbanModule>
{
    private readonly IUrbanDictionaryClient _urbanDictionary = Utils.CreateMockedUrbanDictionaryApi();

    public UrbanModuleTests()
    {
        var options = Utils.CreateMockedFergunOptions();
        var interactive = new InteractiveService(Client, new InteractiveConfig { ReturnAfterSendingPaginator = true });

        SetupModule(new Mock<UrbanModule>(() => new UrbanModule(Logger, Localizer, Emotes, interactive, options, _urbanDictionary)) { CallBase = true });
        ContextMock.SetupGet(x => x.User).Returns(() => AutoFaker.Generate<IUser>(b => b.WithBinder(new MoqBinder())));
    }

    [Theory]
    [MemberData(nameof(GetRandomWords))]
    public async Task SearchAsync_Returns_Definitions(string term)
    {
        var result = await Module.SearchAsync(term);
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        Mock.Get(_urbanDictionary).Verify(u => u.GetDefinitionsAsync(It.Is<string>(x => x == term), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    public async Task SearchAsync_Returns_No_Definitions(string? term)
    {
        var result = await Module.SearchAsync(term!);
        Assert.False(result.IsSuccess);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        Mock.Get(_urbanDictionary).Verify(u => u.GetDefinitionsAsync(It.Is<string>(x => x == term), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RandomAsync_Calls_GetRandomDefinitionsAsync()
    {
        var result = await Module.RandomAsync();
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        Mock.Get(_urbanDictionary).Verify(u => u.GetRandomDefinitionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WordsOfTheDayAsync_Calls_GetWordsOfTheDayAsync()
    {
        var result = await Module.WordsOfTheDayAsync();
        Assert.True(result.IsSuccess);

        InteractionMock.VerifyDeferAsync(false, Times.Once());
        Mock.Get(_urbanDictionary).Verify(u => u.GetWordsOfTheDayAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalid_SearchType_Throws_ArgumentException()
    {
        var task = Module.SearchAndSendAsync((UrbanSearchType)3);

        await Assert.ThrowsAsync<ArgumentException>(() => task);
    }

    public static TheoryData<string> GetRandomWords()
        => Enumerable.Range(0, 10).Select(i => $"word{i}").ToTheoryData();
}