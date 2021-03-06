using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.OpenTriviaDB
{
    public static class TriviaApi
    {
        public const string ApiEndpoint = "https://opentdb.com/api.php";
        public const string ApiTokenEndpoint = "https://opentdb.com/api_token.php";
        public const string ApiCategoryEndpoint = "https://opentdb.com/api_category.php";
        public const string ApiCategoryCountEndpoint = "https://opentdb.com/api_count.php";
        public const string ApiGlobalCountEndpoint = "https://opentdb.com/api_count_global.php";

        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Requests questions from the API.
        /// </summary>
        /// <param name="amount">The amount of questions to request.</param>
        /// <param name="category">The category of the questions. If left empty the questions will have mixed categories.</param>
        /// <param name="difficulty">The difficulty of the questions. Easy, Medium, or Hard. If left empty the questions received will have mixed difficulty levels.</param>
        /// <param name="type">The type of questions. Multiple or Boolean. If left empty the questions will have mixed types.</param>
        /// <param name="encoding">The type of encoding used in the response. Default, urlLegacy, url3986, or base64. If left empty it will use the default encoding (HTML Codes).</param>
        /// <param name="sessionToken">A session token. This token prevents the API from giving you the same question twice until 6 hours of inactivity or you reset the token.</param>
        /// <returns>A <see cref="QuestionsResponse"/> object.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is out of range.</exception>
        public static async Task<QuestionsResponse> RequestQuestionsAsync(uint amount,
                                                         QuestionCategory category = QuestionCategory.Any,
                                                         QuestionDifficulty difficulty = QuestionDifficulty.Any,
                                                         QuestionType type = QuestionType.Any,
                                                         ResponseEncoding encoding = ResponseEncoding.Default,
                                                         string sessionToken = "")
        {
            string json = await _httpClient.GetStringAsync(GenerateApiUrl(amount, category, difficulty, type, encoding, sessionToken));
            return JsonConvert.DeserializeObject<QuestionsResponse>(json);
        }

        public static string GenerateApiUrl(uint amount,
                                            QuestionCategory category = QuestionCategory.Any,
                                            QuestionDifficulty difficulty = QuestionDifficulty.Any,
                                            QuestionType type = QuestionType.Any,
                                            ResponseEncoding encoding = ResponseEncoding.Default,
                                            string sessionToken = "")
        {
            if (amount < 1 || amount > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be between 1 and 50.");
            }

            string query = $"amount={amount}";

            if (category != QuestionCategory.Any)
            {
                query += $"&category={category:D}";
            }

            if (difficulty != QuestionDifficulty.Any)
            {
                query += $"&difficulty={difficulty.ToString().ToLowerInvariant()}";
            }

            if (type != QuestionType.Any)
            {
                query += $"&type={type.ToString().ToLowerInvariant()}";
            }

            if (encoding != ResponseEncoding.Default)
            {
                query += $"&encode={encoding}";
            }

            if (!string.IsNullOrEmpty(sessionToken))
            {
                query += $"&token={sessionToken}";
            }

            return $"{ApiEndpoint}?{query}";
        }

        /// <summary>
        /// Sends a command to the Token API endpoint.
        /// </summary>
        /// <param name="command">The command to send. It can be "Request" (Requests a session token) or "Reset" (Resets the provided session token)</param>
        /// <param name="sessionToken">Resets the provided session token, only if one is passed and command is set to "Reset".</param>
        /// <returns>A <see cref="SessionTokenResponse"/> object.</returns>
        public static async Task<SessionTokenResponse> SendSessionTokenCommandAsync(TokenCommand command, string sessionToken = "")
        {
            string query = $"command={command.ToString().ToLowerInvariant()}";

            if (command == TokenCommand.Reset)
            {
                if (string.IsNullOrEmpty(sessionToken))
                {
                    throw new ArgumentException("You must pass a session token when requesting a reset.", nameof(sessionToken));
                }
                query += $"&token={sessionToken}";
            }

            string json = await _httpClient.GetStringAsync($"{ApiTokenEndpoint}?{query}");
            return JsonConvert.DeserializeObject<SessionTokenResponse>(json);
        }

        /// <summary>
        /// Requests the list of categories and IDs in the database.
        /// </summary>
        /// <returns>A <see cref="CategoryListResponse"/> object.</returns>
        public static async Task<CategoryListResponse> RequestCategoryListAsync()
        {
            string json = await _httpClient.GetStringAsync(ApiCategoryEndpoint);
            return JsonConvert.DeserializeObject<CategoryListResponse>(json);
        }

        /// <summary>
        /// Requests the number of questions in the database, in a specific category.
        /// </summary>
        /// <param name="category">The category to request.</param>
        /// <returns>A <see cref="NumberOfQuestionsInCategoryResponse"/> object.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="category"/> is <see cref="QuestionCategory.Any"/>.</exception>
        public static async Task<NumberOfQuestionsInCategoryResponse> RequestNumberOfQuestionsInCategoryAsync(QuestionCategory category)
        {
            if (category == QuestionCategory.Any)
            {
                throw new ArgumentException("You must specify a category.", nameof(category));
            }

            string json = await _httpClient.GetStringAsync($"{ApiCategoryCountEndpoint}?category={category:D}");
            return JsonConvert.DeserializeObject<NumberOfQuestionsInCategoryResponse>(json);
        }

        /// <summary>
        /// Requests the total number of questions in the database.
        /// </summary>
        /// <returns>A <see cref="GlobalQuestionCountResponse"/> object.</returns>
        public static async Task<GlobalQuestionCountResponse> RequestGlobalQuestionCountAsync()
        {
            string json = await _httpClient.GetStringAsync(ApiGlobalCountEndpoint);
            return JsonConvert.DeserializeObject<GlobalQuestionCountResponse>(json);
        }
    }
}