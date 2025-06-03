namespace Fergun.Data.Models;

/// <summary>
/// Specifies the possible blacklist status of a user.
/// </summary>
public enum BlacklistStatus
{
    /// <summary>
    /// The user is not blacklisted.
    /// </summary>
    None = 0,

    /// <summary>
    /// The user is blacklisted.
    /// </summary>
    Blacklisted = 1,

    /// <summary>
    /// The user is "shadow"-blacklisted. The user shouldn't be notified that they're blacklisted.
    /// </summary>
    ShadowBlacklisted = 2
}