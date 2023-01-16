using Discord;
using Moq;
using System.Collections.Generic;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Fergun.Tests;

public class FergunResultTests
{
    [Theory]
    [MemberData(nameof(GetFergunResultData))]
    public void FergunResult_FromError_Has_Expected_Values(string reason, bool isEphemeral, IDiscordInteraction? interaction)
    {
        var result = FergunResult.FromError(reason, isEphemeral, interaction);

        Assert.Equal(reason, result.ErrorReason);
        Assert.Equal(isEphemeral, result.IsEphemeral);
        Assert.Same(interaction, result.Interaction);
    }

    [Theory]
    [MemberData(nameof(GetLocalizedFergunResultData))]
    public void FergunResult_Localized_FromError_Has_Expected_Values(LocalizedString reason, bool isEphemeral, IDiscordInteraction? interaction)
    {
        var result = FergunResult.FromError(reason, isEphemeral, interaction);

        Assert.Equal((string)reason, result.ErrorReason);
        Assert.Equal(isEphemeral, result.IsEphemeral);
        Assert.Same(reason, result.LocalizedErrorReason);
        Assert.Same(interaction, result.Interaction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Success")]
    public void FergunResult_FromSuccess_Has_Null_Error(string? reason)
    {
        var result = FergunResult.FromSuccess(reason);

        Assert.Null(result.Error);
        Assert.Equal(reason ?? string.Empty, result.ErrorReason);
    }

    [Fact]
    public void FergunResult_FromSilentError_Is_Silent()
    {
        var result = FergunResult.FromSilentError();

        Assert.True(result.IsSilent);
    }

    public static IEnumerable<object?[]> GetFergunResultData()
    {
        var interactionMock = new Mock<IDiscordInteraction>();

        yield return new object?[] { "Error", true, null };
        yield return new object?[] { "Error 2", false, interactionMock.Object };
    }

    public static IEnumerable<object?[]> GetLocalizedFergunResultData()
    {
        var interactionMock = new Mock<IDiscordInteraction>();

        yield return new object?[] { new LocalizedString("ErrorMessage", "Error message"), true, null };
        yield return new object?[] { new LocalizedString("TestMessage", "Test message"), false, interactionMock.Object };
    }
}