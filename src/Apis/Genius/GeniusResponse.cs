using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Fergun.Apis.Genius;

internal record GeniusResponse<TResponse>([property: JsonPropertyName("response")] TResponse Response);

[UsedImplicitly]
internal record GeniusSearchResponse([property: JsonPropertyName("hits")] IReadOnlyList<GeniusSearchHit> Hits);

[UsedImplicitly]
internal record GeniusSearchHit([property: JsonPropertyName("result")] GeniusSong Result);

[UsedImplicitly]
internal record GeniusSongResponse([property: JsonPropertyName("song")] GeniusSong Song);