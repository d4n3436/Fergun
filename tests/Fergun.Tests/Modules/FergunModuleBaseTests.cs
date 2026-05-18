using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Localization;
using Fergun.Modules;
using Fergun.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fergun.Tests.Modules;

public class FergunModuleBaseTests
{
    [Fact]
    public void BeforeExecute_Sets_CurrentCulture_From_Interaction_Locale()
    {
        var localizer = Utils.CreateMockedLocalizer<TestModule>();
        var interactionMock = new Mock<IDiscordInteraction>();
        interactionMock.SetupGet(x => x.UserLocale).Returns("es");
        var contextMock = new Mock<IInteractionContext>();
        contextMock.SetupGet(x => x.Interaction).Returns(interactionMock.Object);

        using var client = new DiscordSocketClient();
        var module = new TestModule(
            Mock.Of<ILogger<TestModule>>(),
            localizer,
            Mock.Of<FergunEmoteProvider>(),
            new InteractiveService(client));

        ((IInteractionModuleBase)module).SetContext(contextMock.Object);

        module.BeforeExecute(Mock.Of<ICommandInfo>());

        Assert.Equal("es", localizer.CurrentCulture.TwoLetterISOLanguageName);
    }

    public sealed class TestModule : FergunModuleBase<TestModule>
    {
        public TestModule(ILogger<TestModule> logger, IFergunLocalizer<TestModule> localizer, FergunEmoteProvider emotes, InteractiveService interactive)
            : base(logger, localizer, emotes, interactive)
        {
        }
    }
}