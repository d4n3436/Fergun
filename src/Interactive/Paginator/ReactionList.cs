namespace Fergun.Interactive
{
    /// <summary>
    /// The reaction list.
    /// </summary>
    public class ReactionList
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReactionList"/> class with the default values.
        /// </summary>
        public ReactionList()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReactionList"/> class with the provided values.
        /// </summary>
        public ReactionList(bool first, bool last, bool forward, bool backward, bool jump, bool stop, bool info)
        {
            First = first;
            Last = last;
            Forward = forward;
            Backward = backward;
            Jump = jump;
            Stop = stop;
            Info = info;
        }

        public bool First { get; set; }
        public bool Last { get; set; }
        public bool Forward { get; set; } = true;
        public bool Backward { get; set; } = true;
        public bool Jump { get; set; }
        public bool Stop { get; set; } = true;
        public bool Info { get; set; }

        /// <summary>
        /// Gets a reaction list with the default settings.
        /// </summary>
        public static ReactionList Default => new ReactionList();

        /// <summary>
        /// Gets a reaction list with all reactions enabled.
        /// </summary>
        public static ReactionList All => new ReactionList(true, true, true, true, true, true, true);
    }
}