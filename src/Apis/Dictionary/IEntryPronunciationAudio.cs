using System;

namespace Fergun.Apis.Dictionary;

/// <summary>
/// Represents the pronunciation audio data.
/// </summary>
public interface IEntryPronunciationAudio
{
    /// <summary>
    /// Gets an <see cref="Uri"/> pointing to the pronunctiation audio file in OGG.
    /// </summary>
    Uri Ogg { get; }

    /// <summary>
    /// Gets an <see cref="Uri"/> pointing to the pronunctiation audio file in MPEG.
    /// </summary>
    Uri Mpeg { get; }
}