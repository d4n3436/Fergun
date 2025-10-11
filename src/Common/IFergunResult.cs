using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace Fergun.Common;

/// <summary>
/// Provides extra information on how command results should be handled.
/// </summary>
public interface IFergunResult : IResult
{
    /// <summary>
    /// Gets the reason of failure as a localized string.
    /// </summary>
    LocalizedString? LocalizedErrorReason { get; }

    /// <summary>
    /// Gets a value indicating whether the response should be ephemeral.
    /// </summary>
    bool IsEphemeral { get; }

    /// <summary>
    /// Gets a value indicating whether the response should be silent.
    /// </summary>
    bool IsSilent { get; }

    /// <summary>
    /// Gets the interaction that should be responded to.
    /// </summary>
    IDiscordInteraction? Interaction { get; }

    /// <summary>
    /// Gets the optional components that will be included in the response.
    /// </summary>
    MessageComponent? Components { get; }
}