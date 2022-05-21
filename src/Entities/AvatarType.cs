using Discord.Interactions;

namespace Fergun;

/// <summary>
/// Specifies the types of avatars.
/// </summary>
public enum AvatarType
{
    /// <summary>
    /// The first available avatar (Server, then Global, then Default).
    /// </summary>
    [Hide]
    FirstAvailable,

    /// <summary>
    /// Server avatar.
    /// </summary>
    [ChoiceDisplay("Server avatar")]
    Server,

    /// <summary>
    /// Global (main) avatar.
    /// </summary>
    [ChoiceDisplay("Global (main) avatar")]
    Global,

    /// <summary>
    /// Default avatar.
    /// </summary>
    [ChoiceDisplay("Default avatar")]
    Default
}