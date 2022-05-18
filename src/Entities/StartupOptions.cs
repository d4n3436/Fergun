namespace Fergun;

/// <summary>
/// Represents startup settings.
/// </summary>
public class StartupOptions
{
    public const string Startup = nameof(Startup);

    /// <summary>
    /// Gets or sets the token of the bot.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the guild to register the commands for testing.
    /// </summary>
    public ulong TestingGuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild to register owner commands.
    /// </summary>
    public ulong OwnerCommandsGuildId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mobile status should be used.
    /// </summary>
    public bool MobileStatus { get; set; }
}