using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class AIDungeon
    {
        // TODO: Add the option to specify a custom API endpoint in the constructor.

        public const string ApiEndpoint = "https://api.aidungeon.io";
        public const uint AllScenarios = 458612; //362833
        private static readonly HttpClient _aidClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public string Token { get; set; }

        public AIDungeon()
        {
        }

        public AIDungeon(string token)
        {
            Token = token;
        }

        private async Task<string> SendApiRequestAsync<T>(T jsonRequest, bool useAccessToken = true) where T : class
        {
            string jsonString = JsonConvert.SerializeObject(jsonRequest);
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/graphql"))
            using (var requestContent = new StringContent(jsonString, Encoding.UTF8, "application/json"))
            {
                if (useAccessToken)
                {
                    if (string.IsNullOrEmpty(Token))
                    {
                        throw new NullReferenceException("Token can't be empty.");
                    }
                    //if (Guid.TryParse(Token, out _))
                    //{
                    //    throw new NullReferenceException("Invalid token.");
                    //}
                    request.Headers.Add("X-Access-Token", Token);
                }
                request.Content = requestContent;
                var response = await _aidClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        public async Task<RegisterResponse> RegisterAsync(string email, string username, string password)
        {
            var request = new RegisterRequest(email, username, password);
            string response = await SendApiRequestAsync(request, false);
            return JsonConvert.DeserializeObject<RegisterResponse>(response);
        }

        public async Task<AnonymousAccountResponse> CreateAnonymousAccountAsync()
        {
            var request = new AnonymousAccountRequest();
            string response = await SendApiRequestAsync(request, false);
            return JsonConvert.DeserializeObject<AnonymousAccountResponse>(response);
        }

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var request = new LoginRequest(email, password);
            string response = await SendApiRequestAsync(request, false);
            return JsonConvert.DeserializeObject<LoginResponse>(response);
        }

        public async Task<ForgotPasswordResponse> SendForgotPasswordEmailAsync(string email)
        {
            var request = new ForgotPasswordRequest(email);
            string response = await SendApiRequestAsync(request, false);
            return JsonConvert.DeserializeObject<ForgotPasswordResponse>(response);
        }

        public async Task<CreationResponse> CreateAdventureAsync(uint scenarioId, string prompt = null)
        {
            var request = new CreationRequest(scenarioId, prompt);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<CreationResponse>(response);
        }

        public async Task<ScenarioResponse> GetScenarioAsync(uint ScenarioId)
        {
            var request = new ScenarioRequest(ScenarioId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<ScenarioResponse>(response);
        }

        public async Task<AdventureListResponse> GetAdventureListAsync()
        {
            var request = new AdventureListRequest();
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<AdventureListResponse>(response);
        }

        public async Task<RefreshResponse> RefreshAdventureListAsync()
        {
            var request = new RefreshRequest();
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<RefreshResponse>(response);
        }

        public async Task<AdventureInfoResponse> GetAdventureAsync(uint adventureId)
        {
            var request = new AdventureInfoRequest(adventureId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<AdventureInfoResponse>(response);
        }

        public async Task<ActionResponse> RunActionAsync(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            var request = new ActionRequest(adventureId, action, text, actionId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<ActionResponse>(response);
        }

        public async Task<DeleteResponse> DeleteAdventureAsync(uint adventureId)
        {
            var request = new DeleteRequest(adventureId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<DeleteResponse>(response);
        }
    }
}