using System.Diagnostics.CodeAnalysis;

namespace Fergun.Tests.Apis;

public static class UrbanTestData
{
    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string DefinitionsResponse =
        """
        {
          "list": [
            {
              "definition": "A procedure intended to establish the quality, performance, or reliability of something.",
              "date": null,
              "permalink": "https://test.urbanup.com/123",
              "thumbs_up": 42,
              "sound_urls": [],
              "author": "tester",
              "word": "test",
              "defid": 123,
              "written_on": "2020-01-02T03:04:05.000Z",
              "example": "This is a [test] of the parser.",
              "thumbs_down": 7
            },
            {
              "definition": "A second definition for the same word.",
              "date": "2021-05-06",
              "permalink": "https://test.urbanup.com/456",
              "thumbs_up": 3,
              "sound_urls": [],
              "author": "another",
              "word": "test",
              "defid": 456,
              "written_on": "2021-05-06T07:08:09.000Z",
              "example": "Another [test] example.",
              "thumbs_down": 1
            }
          ]
        }
        """;
}