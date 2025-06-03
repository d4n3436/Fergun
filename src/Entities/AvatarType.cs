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
    FirstAvailable = 0,

    /// <summary>
    /// Server avatar.
    /// </summary>
    [ChoiceDisplay("Server avatar")]
    Server = 1,

    /// <summary>
    /// Global (main) avatar.
    /// </summary>
    [ChoiceDisplay("Global (main) avatar")]
    Global = 2,

    /// <summary>
    /// Default avatar.
    /// </summary>
    [ChoiceDisplay("Default avatar")]
    Default = 3
}