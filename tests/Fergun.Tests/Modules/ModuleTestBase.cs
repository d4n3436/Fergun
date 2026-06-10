using System;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Localization;
using Fergun.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fergun.Tests.Modules;

public abstract class ModuleTestBase<TModule> : IDisposable
    where TModule : class, IInteractionModuleBase
{
    protected readonly Mock<IInteractionContext> ContextMock = new();
    protected readonly Mock<IDiscordInteraction> InteractionMock = new();
    protected readonly IFergunLocalizer<TModule> Localizer = Utils.CreateMockedLocalizer<TModule>();
    protected readonly FergunEmoteProvider Emotes = Mock.Of<FergunEmoteProvider>();
    protected readonly ILogger<TModule> Logger = Mock.Of<ILogger<TModule>>();
    protected readonly DiscordSocketClient Client = new();

    private bool _disposed;

    protected Mock<TModule> ModuleMock { get; private set; } = null!;

    protected TModule Module => ModuleMock.Object;
    
    protected void SetupModule(Mock<TModule> moduleMock)
    {
        ModuleMock = moduleMock;
        ContextMock.SetupGet(x => x.Interaction).Returns(InteractionMock.Object);
        moduleMock.Object.SetContext(ContextMock.Object);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}