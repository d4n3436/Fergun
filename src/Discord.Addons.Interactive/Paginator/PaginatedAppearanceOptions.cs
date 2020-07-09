using System;

namespace Discord.Addons.Interactive
{
    /// <summary>
    /// Specifies the options when the jump reactions will be displayed.
    /// </summary>
    public enum JumpDisplayOptions
    {
        Never,
        WithManageMessages,
        Always
    }

    /// <summary>
    /// Specifies the action to do when the timeout is reached or the user reacts to the stop button (if present).
    /// Regardless of the option, the hook/callback will be removed from the message.
    /// </summary>
    public enum ActionOnTimeout
    {
        /// <summary>
        /// Remove the message.
        /// </summary>
        DeleteMessage,

        /// <summary>
        /// Remove the reactions.
        /// </summary>
        DeleteReactions,

        /// <summary>
        /// Remove the hook only.
        /// </summary>
        Nothing
    }

    /// <summary>
    /// The paginated appearance options.
    /// </summary>
    public class PaginatedAppearanceOptions
    {
        /// <summary>
        /// Get or sets the emote that sends to the first page.
        /// </summary>
        public IEmote First { get; set; } = new Emoji("⏮");

        /// <summary>
        /// Get or sets the emote that sends to the previous page.
        /// </summary>
        public IEmote Back { get; set; } = new Emoji("⬅"); //◀

        /// <summary>
        /// Get or sets the emote that sends to the next page.
        /// </summary>
        public IEmote Next { get; set; } = new Emoji("➡"); //▶

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
        /// Get or sets the information that will be shown when the <see cref="Info"/> emote is pressed.
        /// </summary>
        public string InformationText { get; set; } = "This is a paginator. React with the respective icons to change page.";

        /// <summary>
        /// The display options for the <see cref="Jump"/> emote.
        /// </summary>
        public JumpDisplayOptions JumpDisplayOptions { get; set; } = JumpDisplayOptions.WithManageMessages;

        /// <summary>
        /// Get or sets the paginator timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; } = null;

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
        /// Gets this class with the default settings.
        /// </summary>
        public static PaginatedAppearanceOptions Default => new PaginatedAppearanceOptions();
    }
}