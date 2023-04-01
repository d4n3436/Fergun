using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents the default implementation of <see cref="IFergunResult"/> for command preconditions.
/// </summary>
public class FergunPreconditionResult : PreconditionResult, IFergunResult
{
    private FergunPreconditionResult(InteractionCommandError? error, string reason, bool isEphemeral, bool isSilent, IDiscordInteraction? interaction)
        : base(error, reason)
    {
        IsEphemeral = isEphemeral;
        IsSilent = isSilent;
        Interaction = interaction;
    }

    private FergunPreconditionResult(InteractionCommandError? error, LocalizedString reason, bool isEphemeral, bool isSilent, IDiscordInteraction? interaction)
        : this(error, (string)reason, isEphemeral, isSilent, interaction)
    {
        LocalizedErrorReason = reason;
    }

    /// <inheritdoc />
    public LocalizedString? LocalizedErrorReason { get; }

    /// <inheritdoc />
    public bool IsEphemeral { get; }

    /// <inheritdoc />
    public bool IsSilent { get; }

    /// <inheritdoc />
    public IDiscordInteraction? Interaction { get; }

    /// <summary>
    /// Creates a successful instance of the <see cref="FergunPreconditionResult"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <returns>A <see cref="FergunPreconditionResult"/>.</returns>
    public static FergunPreconditionResult FromSuccess(string? reason = null) => new(null, reason ?? string.Empty, false, false, null);

    /// <summary>
    /// Creates a <see cref="FergunPreconditionResult"/> with error type <see cref="InteractionCommandError.UnmetPrecondition"/>.
    /// </summary>
    /// <param name="reason">The reason of the result.</param>
    /// <param name="isEphemeral">Whether the response should be ephemeral.</param>
    /// <param name="interaction">The interaction that should be responded to.</param>
    /// <returns>A <see cref="FergunPreconditionResult"/>.</returns>
    public static FergunPreconditionResult FromError(LocalizedString reason, bool isEphemeral = false, IDiscordInteraction? interaction = null)
        => new(InteractionCommandError.UnmetPrecondition, reason, isEphemeral, false, interaction);
}