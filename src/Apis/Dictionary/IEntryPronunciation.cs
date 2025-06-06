namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the pronunciation data of a dictionary entry.
/// </summary>
public interface IEntryPronunciation
{
    /// <summary>
    /// Gets the IPA transcription.
    /// </summary>
    string Ipa { get; }
}