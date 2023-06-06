using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fergun.Apis.Genius;

internal record GeniusResponse<TResponse>([property: JsonPropertyName("response")] TResponse Response);

internal record GeniusSearchResponse([property: JsonPropertyName("hits")] IReadOnlyList<GeniusSearchHit> Hits);

internal record GeniusSearchHit([property: JsonPropertyName("result")] GeniusSong Result);

internal record GeniusSongResponse([property: JsonPropertyName("song")] GeniusSong Song);