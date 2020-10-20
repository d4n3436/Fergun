using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.OpenTriviaDB
{
    // Data classes
    // These contain information about a certain part of a request response
    public class QuestionData
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("question")]
        public string Question { get; set; }

        [JsonProperty("correct_answer")]
        public string CorrectAnswer { get; set; }

        [JsonProperty("incorrect_answers")]
        public List<string> IncorrectAnswers { get; set; }
    }

    public class CategoryData
    {
        [JsonProperty("id")]
        public uint Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class CategoryQuestionCount
    {
        [JsonProperty("total_question_count")]
        public uint TotalQuestionCount { get; set; }

        [JsonProperty("total_easy_question_count")]
        public uint EasyQuestionCount { get; set; }

        [JsonProperty("total_medium_question_count")]
        public uint MediumQuestionCount { get; set; }

        [JsonProperty("total_hard_question_count")]
        public uint HardQuestionCount { get; set; }
    }

    public class CategoryQuestionData
    {
        [JsonProperty("total_num_of_questions")]
        public uint TotalNumberOfQuestions { get; set; }

        [JsonProperty("total_num_of_pending_questions")]
        public uint TotalNumberOfPendingQuestions { get; set; }

        [JsonProperty("total_num_of_verified_questions")]
        public uint TotalNumberOfVerifiedQuestions { get; set; }

        [JsonProperty("total_num_of_rejected_questions")]
        public uint TotalNumberOfRejectedQuestions { get; set; }
    }

    /** Response Classes **/
    // When you send a request to the API you will get an instance of one of these classes as a response

    public class QuestionsResponse
    {
        [JsonProperty("response_code")]
        public uint ResponseCode { get; set; }

        [JsonProperty("results")]
        public List<QuestionData> Questions { get; set; }
    }

    public class SessionTokenResponse
    {
        [JsonProperty("response_code")]
        public string ResponseCode { get; set; }

        // This value isn't included on a token reset command.
        [JsonProperty("response_message", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponseMessage { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }

    public class CategoryListResponse
    {
        [JsonProperty("trivia_categories")]
        public List<CategoryData> CategoryList { get; set; }
    }

    public class NumberOfQuestionsInCategoryResponse
    {
        [JsonProperty("category_id")]
        public uint CategoryId { get; set; }

        [JsonProperty("category_question_count")]
        public CategoryQuestionCount CategoryQuestionCount { get; set; }
    }

    public class GlobalQuestionCountResponse
    {
        [JsonProperty("overall")]
        public CategoryQuestionData Overall { get; set; }

        [JsonProperty("categories")]
        public Dictionary<uint, CategoryQuestionData> CategoriesQuestionCount { get; set; }
    }
}