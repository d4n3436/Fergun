using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents a primary artist.
/// </summary>
/// <param name="Url">A URL pointing to the primary artist page.</param>
[UsedImplicitly]
public record GeniusPrimaryArtist([property: JsonPropertyName("url")] string Url);