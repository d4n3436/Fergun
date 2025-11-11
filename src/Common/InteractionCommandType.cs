using Discord.Interactions;

namespace Fergun.Common;

/// <summary>
/// Specifies the types of commands in the <see cref="InteractionService"/>.
/// </summary>
public enum InteractionCommandType
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