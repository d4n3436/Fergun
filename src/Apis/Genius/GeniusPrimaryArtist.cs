﻿using System.Text.Json.Serialization;

namespace Fergun.Apis.Genius;

/// <summary>
/// Represents a primary artist.
/// </summary>
/// <param name="Url">A URL pointing to the primary artist page.</param>
public record GeniusPrimaryArtist([property: JsonPropertyName("url")] string Url);