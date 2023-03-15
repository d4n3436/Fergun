using System.Collections.Generic;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the pronunctiation data of a dictionary entry.
/// </summary>
public interface IEntryPronunciation
{
    /// <summary>
    /// Gets the IPA transcription.
    /// </summary>
    string Ipa { get; }

    /// <summary>
    /// Gets a read-only list containg the spellings.
    /// </summary>
    IReadOnlyList<string> Spell { get; }

    /// <summary>
    /// Gets the audio data.
    /// </summary>
    IEntryPronunciationAudio? Audio { get; }
}