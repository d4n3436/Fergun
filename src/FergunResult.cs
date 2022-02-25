using Discord.Interactions;

namespace Fergun;

public class FergunResult : RuntimeResult
{
    /// <inheritdoc />
    private FergunResult(InteractionCommandError? error, string reason) : base(error, reason)
    {
    }

    public static FergunResult FromSuccess(string? reason = null) => new(null, reason ?? "");

    public static FergunResult FromError(string reason) => new(InteractionCommandError.Unsuccessful, reason);
}