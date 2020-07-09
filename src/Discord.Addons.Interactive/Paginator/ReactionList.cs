namespace Discord.Addons.Interactive
{
    /// <summary>
    /// The reaction list.
    /// </summary>
    public class ReactionList
    {
        public ReactionList()
        {
        }

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

        public bool First { get; set; } = false;
        public bool Last { get; set; } = false;
        public bool Forward { get; set; } = true;
        public bool Backward { get; set; } = true;
        public bool Jump { get; set; } = false;
        public bool Stop { get; set; } = true;
        public bool Info { get; set; } = false;

        /// <summary>
        /// Returns a reaction list with the default settings.
        /// </summary>
        public static ReactionList Default => new ReactionList();

        /// <summary>
        /// Returns a reaction list with all reactions enabled.
        /// </summary>
        public static ReactionList All => new ReactionList(true, true, true, true, true, true, true);
    }
}