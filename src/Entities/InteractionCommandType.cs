using Discord.Interactions;

namespace Fergun;

/// <summary>
/// Specifies the types of commands in the <see cref="InteractionService"/>.
/// </summary>
public enum InteractionCommandType : byte
{
    /// <summary>
    /// A Slash command.
    /// </summary>
    Slash = 1,

    /// <summary>
    /// A context menu user command.
    /// </summary>
    User = 2,

    /// <summary>
    /// A context menu message command.
    /// </summary>
    Message = 3,

    /// <summary>
    /// A component command.
    /// </summary>
    Component = 4,

    /// <summary>
    /// A modal command.
    /// </summary>
    Modal = 5
}