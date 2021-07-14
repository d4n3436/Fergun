namespace Fergun.Interactive
{
    /// <summary>
    /// Specifies the possible status of a <see cref="InteractiveResult{T}"/>.
    /// </summary>
    public enum InteractiveStatus
    {
        /// <summary>
        /// The interactive action status is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// The interactive action was successful.
        /// </summary>
        Success,
        /// <summary>
        /// The interactive action timed out.
        /// </summary>
        TimedOut,
        /// <summary>
        /// The interactive action was canceled.
        /// </summary>
        Canceled
    }
}