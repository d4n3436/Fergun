using System;
using Discord;

namespace Fergun.Interactive
{
    /// <summary>
    /// Represents the paginator appearance options.
    /// </summary>
    public class PaginatorAppearanceOptions
    {
        /// <summary>
        /// Get or sets the emote that sends to the first page.
        /// </summary>
        public IEmote First { get; set; } = new Emoji("⏮");

        /// <summary>
        /// Get or sets the emote that sends to the previous page.
        /// </summary>
        public IEmote Back { get; set; } = new Emoji("⬅");

        /// <summary>
        /// Get or sets the emote that sends to the next page.
        /// </summary>
        public IEmote Next { get; set; } = new Emoji("➡");

        /// <summary>
        /// Get or sets the emote that sends to the last page.
        /// </summary>
        public IEmote Last { get; set; } = new Emoji("⏭");

        /// <summary>
        /// Get or sets the emote that stops the paginator.
        /// </summary>
        public IEmote Stop { get; set; } = new Emoji("⏹");

        /// <summary>
        /// Get or sets the emote that jumps to a certain page.
        /// </summary>
        public IEmote Jump { get; set; } = new Emoji("🔢");

        /// <summary>
        /// Get or sets the emote that sends the text in <see cref="InformationText"/>.
        /// </summary>
        public IEmote Info { get; set; } = new Emoji("ℹ");

        /// <summary>
        /// Get or sets the format of the embed footer.
        /// </summary>
        public string FooterFormat { get; set; } = "Page {0}/{1}";

        /// <summary>
        /// Get or sets the information that will be displayed when the <see cref="Info"/> emote is pressed.
        /// </summary>
        public string InformationText { get; set; } = "This is a paginator. React with the respective icons to change page.";

        /// <summary>
        /// Get or sets the paginator timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Get or sets the timeout for the information text.
        /// </summary>
        public TimeSpan InfoTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Get or sets the action to do when the paginator reaches the timeout.
        /// </summary>
        public ActionOnTimeout ActionOnTimeout { get; set; } = ActionOnTimeout.DeleteMessage;

        /// <summary>
        /// Get or sets the action to do when the <see cref="Stop"/> emote is pressed.
        /// </summary>
        public ActionOnTimeout ActionOnStop { get; set; } = ActionOnTimeout.DeleteMessage;

        /// <summary>
        /// Gets an instance of this class with the default settings.
        /// </summary>
        public static PaginatorAppearanceOptions Default => new PaginatorAppearanceOptions();
    }

    /// <summary>
    /// Specifies the action to do when the timeout is reached, or the user reacts to the stop button (if present).
    /// </summary>
    /// <remarks>Regardless of the option, the hook/callback will be removed from the message.</remarks>
    public enum ActionOnTimeout
    {
        /// <summary>
        /// Do nothing.
        /// </summary>
        Nothing,

        /// <summary>
        /// Delete the reactions.
        /// </summary>
        DeleteReactions,

        /// <summary>
        /// Delete the message.
        /// </summary>
        DeleteMessage
    }
}