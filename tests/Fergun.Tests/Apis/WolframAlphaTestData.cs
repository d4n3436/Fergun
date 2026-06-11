using System.Diagnostics.CodeAnalysis;

namespace Fergun.Tests.Apis;

public static class WolframAlphaTestData
{
    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string AutocompleteResponse =
        """
        {
          "results": [
            {
              "input": "2 + 2"
            },
            {
              "input": "2 + 3"
            }
          ]
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string SuccessResponse =
        """
        {
          "queryresult": {
            "success": true,
            "warnings": {
              "text": "Interpreting as a calculation."
            },
            "pods": [
              {
                "id": "Input",
                "title": "Input",
                "subpods": [
                  {
                    "img": {
                      "src": "https://example.com/i.gif",
                      "width": 100,
                      "height": 20,
                      "contenttype": "image/gif"
                    },
                    "plaintext": "2 + 2",
                    "title": ""
                  }
                ]
              }
            ]
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string DidYouMeanResponse =
        """
        {
          "queryresult": {
            "success": false,
            "didyoumeans": {
              "score": "0.5",
              "level": "medium",
              "val": "kitten"
            }
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string FutureTopicResponse =
        """
        {
          "queryresult": {
            "success": false,
            "futuretopic": {
              "topic": "Microsoft Windows",
              "msg": "Development of this topic is under investigation."
            }
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string NoResultResponse =
        """
        {
          "queryresult": {
            "success":false,
            "pods": [
              
            ]
          }
        }
        """;

    [StringSyntax(StringSyntaxAttribute.Json)]
    public const string ErrorResponse =
        """
        {
          "queryresult": {
            "success": false,
            "error": {
              "code": "1000",
              "msg": "error message"
            }
          }
        }
        """;
}