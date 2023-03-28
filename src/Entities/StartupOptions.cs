namespace Fergun;

/// <summary>
/// Represents startup settings.
/// </summary>
public class StartupOptions
{
    /// <summary>
    /// Returns the constant "Startup".
    /// </summary>
    public const string Startup = nameof(Startup);

    /// <summary>
    /// Gets the token of the bot.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ID of the guild to register the commands for testing.
    /// </summary>
    public ulong TestingGuildId { get; init; }

    /// <summary>
    /// Gets the ID of the guild to register owner commands.
    /// </summary>
    public ulong OwnerCommandsGuildId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the mobile status should be used.
    /// </summary>
    public bool MobileStatus { get; init; }
}