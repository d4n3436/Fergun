using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Common;
using Fergun.Localization;
using Fergun.Preconditions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Fergun.Tests.Preconditions;

public class RatelimitAttributeTests
{
    private const ulong OwnerId = 1;

    private readonly IServiceProvider _services = new ServiceCollection()
        .AddSingleton(Utils.CreateMockedLocalizer<SharedResource>())
        .BuildServiceProvider();

    [Fact]
    public async Task Owner_Is_Never_Ratelimited()
    {
        var attribute = new RatelimitAttribute(1, 60);
        var context = CreateContext(OwnerId);

        for (int i = 0; i < 5; i++)
        {
            var result = await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services);
            Assert.True(result.IsSuccess);
        }
    }

    [Fact]
    public async Task Allows_Up_To_Limit_Then_Ratelimits_Ephemerally()
    {
        var attribute = new RatelimitAttribute(2, 60);
        var context = CreateContext(userId: 100);

        Assert.True((await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services)).IsSuccess);
        Assert.True((await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services)).IsSuccess);

        var ratelimitedResult = await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services);

        Assert.False(ratelimitedResult.IsSuccess);
        Assert.Equal(InteractionCommandError.UnmetPrecondition, ratelimitedResult.Error);
        Assert.True(Assert.IsType<FergunPreconditionResult>(ratelimitedResult).IsEphemeral);
    }

    [Fact]
    public async Task Separate_Users_Have_Independent_Limits()
    {
        var attribute = new RatelimitAttribute(1, 60);

        Assert.True((await attribute.CheckRequirementsAsync(CreateContext(10), Mock.Of<ICommandInfo>(), _services)).IsSuccess);
        // A different user must not be affected by the first user's usage
        Assert.True((await attribute.CheckRequirementsAsync(CreateContext(11), Mock.Of<ICommandInfo>(), _services)).IsSuccess);
    }

    [Fact]
    public async Task Resets_After_Period_Elapses()
    {
        var attribute = new RatelimitAttribute(1, 0.3); // 300 ms window
        var context = CreateContext(200);

        Assert.True((await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services)).IsSuccess);
        Assert.False((await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services)).IsSuccess);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.True((await attribute.CheckRequirementsAsync(context, Mock.Of<ICommandInfo>(), _services)).IsSuccess);
    }

    private static IInteractionContext CreateContext(ulong userId)
    {
        var owner = new Mock<IUser>();
        owner.SetupGet(x => x.Id).Returns(OwnerId);

        var app = new Mock<IApplication>();
        app.SetupGet(x => x.Owner).Returns(owner.Object);

        var client = new Mock<IDiscordClient>();
        client.Setup(x => x.GetApplicationInfoAsync(It.IsAny<RequestOptions>())).ReturnsAsync(app.Object);

        var user = new Mock<IUser>();
        user.SetupGet(x => x.Id).Returns(userId);

        var interaction = new Mock<IDiscordInteraction>();
        interaction.SetupGet(x => x.UserLocale).Returns("en");

        var context = new Mock<IInteractionContext>();
        context.SetupGet(x => x.Client).Returns(client.Object);
        context.SetupGet(x => x.User).Returns(user.Object);
        context.SetupGet(x => x.Interaction).Returns(interaction.Object);

        return context.Object;
    }
}