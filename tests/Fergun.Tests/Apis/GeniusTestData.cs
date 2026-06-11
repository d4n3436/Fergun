using System.Diagnostics.CodeAnalysis;

namespace Fergun.Tests.Apis;

public static class GeniusTestData
{
    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string SongResponse =
        """
        {
          "response": {
            "song": {
              "artist_names": "Eminem",
              "primary_artist_names": "Eminem",
              "id": 235729,
              "instrumental": false,
              "lyrics_state": "complete",
              "song_art_image_url": "https://images.genius.com/art.jpg",
              "song_art_primary_color": null,
              "title": "Rap God",
              "url": "https://genius.com/Eminem-rap-god-lyrics",
              "spotify_uuid": "6or1bKJiZ06IlK0vFvY75k",
              "lyrics": { "dom": { "tag": "root", "children": ["Look, I was gonna go easy on you"] } },
              "primary_artist": { "url": "https://genius.com/artists/Eminem" }
            }
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string SearchResponse =
        """
        {
          "response": {
            "hits": [
              {
                "result": {
                  "artist_names": "Eminem",
                  "primary_artist_names": "Eminem",
                  "id": 235729,
                  "instrumental": false,
                  "lyrics_state": "complete",
                  "song_art_image_url": "https://images.genius.com/art.jpg",
                  "song_art_primary_color": null,
                  "title": "Rap God",
                  "url": "https://genius.com/Eminem-rap-god-lyrics",
                  "spotify_uuid": null,
                  "lyrics": null,
                  "primary_artist": { "url": "https://genius.com/artists/Eminem" }
                }
              }
            ]
          }
        }
        """;
}