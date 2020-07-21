namespace Fergun.APIs.AIDungeon
{
    public enum ActionType
    {
        /// <summary>
        /// Do an action.
        /// </summary>
        Do,

        /// <summary>
        /// Say something.
        /// </summary>
        Say,

        /// <summary>
        /// Describe the place.
        /// </summary>
        Story,

        /// <summary>
        /// Generate more story (no input).
        /// </summary>
        Continue,

        /// <summary>
        /// Undo the last action.
        /// </summary>
        Undo,

        /// <summary>
        /// Redo the last action.
        /// </summary>
        Redo,

        /// <summary>
        /// Edit the last action.
        /// </summary>
        Alter,

        /// <summary>
        /// Edit the memory context.
        /// </summary>
        Remember,

        /// <summary>
        /// Retry the last action and generate a new response.
        /// </summary>
        Retry
    }
}