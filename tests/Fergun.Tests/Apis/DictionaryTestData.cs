using System.Diagnostics.CodeAnalysis;

namespace Fergun.Tests.Apis;

public static class DictionaryTestData
{
    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string DefinitionsResponse =
        """
        {
          "data": {
            "content": {
              "luna": {
                "source": "luna",
                "entries": [
                  {
                    "entry": "test",
                    "homograph": 1,
                    "pronunciation": { "ipa": "tɛst" },
                    "posBlocks": [
                      {
                        "definitions": [
                          {
                            "predefinitionContent": "",
                            "postdefinitionContent": "",
                            "definition": "a procedure intended to establish the quality of something",
                            "subdefinitions": []
                          }
                        ],
                        "pos": "noun",
                        "posSupplementaryInfo": null
                      }
                    ],
                    "origin": "Latin testum"
                  }
                ]
              },
              "collins": null
            }
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string SearchResponse =
        """
        {
          "data": [
            {
              "displayText": "test",
              "reference": {
                "identifier": "test",
                "type": "luna"
              }
            }
          ]
        }
        """;
}