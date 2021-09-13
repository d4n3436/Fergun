using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.Tests
{
    // AID GraphQL API wrapper with some extra methods and classes
    internal static class AidGraphQlApi
    {
        public static async Task<string> SendApiRequestAsync<T>(T jsonRequest, string accessToken = null) where T : class
        {
            string jsonString = JsonConvert.SerializeObject(jsonRequest);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.aidungeon.io/graphql");
            using var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
            using var httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("X-Access-Token", accessToken);

            request.Content = requestContent;
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<AnonymousAccountResponse> CreateAnonymousAccountAsync()
        {
            var request = new AnonymousAccountRequest();
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<AnonymousAccountResponse>(response);
        }

        public static async Task<AccountInfoResponse> GetAccountInfoAsync(string accessToken)
        {
            var request = new AccountInfoRequest();
            string response = await SendApiRequestAsync(request, accessToken);
            return JsonConvert.DeserializeObject<AccountInfoResponse>(response);
        }

        public static async Task<AccountGameSettingsResponse> DisableSafeModeAsync(string accessToken, string id, bool nsfwGeneration)
        {
            var request = new AccountGameSettingsRequest(id, nsfwGeneration);
            string response = await SendApiRequestAsync(request, accessToken);
            return JsonConvert.DeserializeObject<AccountGameSettingsResponse>(response);
        }

        public class AnonymousAccountRequest
        {
            [JsonProperty("variables")]
            public AnonymousAccountVariables Variables { get; set; } = new AnonymousAccountVariables();

            [JsonProperty("query")]
            public string Query { get; set; } = "mutation {\n  createAnonymousAccount {\n    id\n    accessToken\n    __typename\n  }\n}\n";
        }

        public class AnonymousAccountVariables
        {
        }

        public class AnonymousAccountResponse
        {
            public AccountData Data { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<ErrorInfo> Errors { get; set; }
        }

        public class AccountData
        {
            public AccountInfo CreateAnonymousAccount { get; set; }
        }

        public class AccountInfo
        {
            public string Id { get; set; }

            public string AccessToken { get; set; }

            [JsonProperty("__typename")]
            public string Typename { get; set; }
        }

        public class ErrorInfo
        {
            public string Message { get; set; }

            public List<Location> Locations { get; set; }

            public List<string> Path { get; set; }

            public Extensions Extensions { get; set; }
        }

        public class Location
        {
            public int Line { get; set; }

            public int Column { get; set; }
        }

        public class Extensions
        {
            public string Code { get; set; }
        }

        public class AccountGameSettingsRequest
        {
            public AccountGameSettingsRequest(string id, bool nsfwGeneration)
            {
                Variables = new AccountGameSettingsVariables
                {
                    Input = new AccountGameSettingsInput { Id = id, NsfwGeneration = nsfwGeneration }
                };
            }

            [JsonProperty("variables")]
            public AccountGameSettingsVariables Variables { get; set; }

            [JsonProperty("query")]
            public string Query { get; set; } = "mutation ($input: GameSettingsInput) {\n  saveGameSettings(input: $input) {\n    id\n    gameSettings {\n      id\n      ...GameSettingsGameSettings\n      ...DisplayGameSettings\n      __typename\n    }\n    __typename\n  }\n}\n\nfragment GameSettingsGameSettings on GameSettings {\n  temperature\n  bannedWords\n  modelType\n  showCommands\n  showModes\n  defaultMode\n  showTips\n  showFeedback\n  textLength\n  playMusic\n  musicVolume\n  playNarration\n  narrationVolume\n  voiceGender\n  voiceAccent\n  voiceId\n  commandList\n  alignCommands\n  nsfwGeneration\n  unrestrictedInput\n  actionScoreOptIn\n  showIconText\n  trainTheAi\n  enableAlpha\n  enableBeta\n  outputWeights\n  __typename\n}\n\nfragment DisplayGameSettings on GameSettings {\n  safeMode\n  textSpeed\n  textFont\n  textSize\n  displayTheme\n  displayColors\n  displayScreen\n  webActionWindowSize\n  mobileActionWindowSize\n  adventureDisplayMode\n  energyBarDisplayMode\n  energyBarAppearance\n  __typename\n}\n";
        }

        public class AccountGameSettingsVariables
        {
            [JsonProperty("input")]
            public AccountGameSettingsInput Input { get; set; }
        }

        public class AccountGameSettingsInput
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("nsfwGeneration")]
            public bool NsfwGeneration { get; set; }
        }

        public class AccountGameSettingsResponse
        {
            public object Data { get; set; } // Irrelevant data

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<ErrorInfo> Errors { get; set; }
        }

        public class AccountInfoRequest
        {
            [JsonProperty("variables")]
            public AccountInfoVariables Variables { get; set; } = new AccountInfoVariables();

            [JsonProperty("query")]
            public string Query { get; set; } = "{\n  user {\n    id\n    username\n    indexNeededCount\n    ...ContentListUser\n    __typename\n  }\n}\n\nfragment ContentListUser on User {\n  ...ContentCardUser\n  __typename\n}\n\nfragment ContentCardUser on User {\n  id\n  username\n  gameSettings {\n    id\n    nsfwGeneration\n    unrestrictedInput\n    __typename\n  }\n  ...ContentOptionsUser\n  ...ContentStatsUser\n  ...SaveButtonUser\n  __typename\n}\n\nfragment ContentOptionsUser on User {\n  id\n  gameSettings {\n    id\n    nsfwGeneration\n    unrestrictedInput\n    __typename\n  }\n  __typename\n}\n\nfragment ContentStatsUser on User {\n  id\n  username\n  __typename\n}\n\nfragment SaveButtonUser on User {\n  id\n  username\n  __typename\n}\n";
        }

        public class AccountInfoVariables
        {
        }

        public class AccountInfoResponse
        {
            public AccountInfoData Data { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<ErrorInfo> Errors { get; set; }
        }

        public class AccountInfoData
        {
            public User User { get; set; }
        }

        public class User
        {
            public string Id { get; set; }

            public object Username { get; set; }

            public long IndexNeededCount { get; set; }

            public GameSettings GameSettings { get; set; }

            [JsonProperty("__typename")]
            public string Typename { get; set; }
        }

        public class GameSettings
        {
            public string Id { get; set; }

            public bool NsfwGeneration { get; set; }

            public bool UnrestrictedInput { get; set; }

            [JsonProperty("__typename")]
            public string Typename { get; set; }
        }
    }
}