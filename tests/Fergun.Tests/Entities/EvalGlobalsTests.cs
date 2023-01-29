using Discord;
using Moq;
using System;
using Xunit;

namespace Fergun.Tests.Entities;

public class EvalGlobalsTests
{
    [Fact]
    public void EvalGlobals_Properties_Has_Expected_Values()
    {
        var contextMock = new Mock<IInteractionContext>();
        var providerMock = new Mock<IServiceProvider>();

        var globals = new EvalGlobals(contextMock.Object, providerMock.Object);

        Assert.Same(globals.Context, contextMock.Object);
        Assert.Same(globals.Services, providerMock.Object);
    }
}