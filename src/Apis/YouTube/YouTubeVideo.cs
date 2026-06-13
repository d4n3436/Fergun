using System;

namespace Fergun.Apis.YouTube;

/// <summary>
/// Represents a YouTube video returned by a search query.
/// </summary>
/// <param name="Id">The ID of the video.</param>
/// <param name="Title">The title of the video.</param>
/// <param name="Author">The name of the channel that uploaded the video.</param>
/// <param name="Duration">The duration of the video, or <see langword="null"/> if it's not available (e.g. a live stream).</param>
public record YouTubeVideo(string Id, string Title, string Author, TimeSpan? Duration)
{
    /// <summary>
    /// Gets the URL of this video.
    /// </summary>
    public string Url => $"https://www.youtube.com/watch?v={Id}";
}