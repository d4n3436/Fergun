using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class AIDungeon
    {
        // TODO: Add the option to specify a custom API endpoint in the constructor.

        public const string ApiEndpoint = "https://api.aidungeon.io";
        public const string WebSocketEndpoint = "wss://api.aidungeon.io/subscriptions";
        public const uint AllScenarios = 458612; //362833
        public const string AllScenariosId = "edd5fdc0-9c81-11ea-a76c-177e6c0711b5";
        private static readonly HttpClient _aidClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public string Token { get; set; }

        public AIDungeon()
        {
        }

        public AIDungeon(string token)
        {
            Token = token;
        }

        private async Task<WebSocketResponse> SendWebSocketRequestInternalAsync(WebSocketRequest request, bool subscribeAdventure)
        {
            if (string.IsNullOrEmpty(Token))
            {
                throw new NullReferenceException("Token can't be empty.");
            }

            // I've tried to reuse the websocket but for some reason, half of the times the response containing the history list don't have the generated text, idk why
            //await StartWebSocketAsync();

            ClientWebSocket _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol("graphql-ws");

            await _webSocket.ConnectAsync(new Uri(WebSocketEndpoint), CancellationToken.None);

            string initData = "{\"type\":\"connection_init\",\"payload\":{\"token\":\"" + Token + "\"}}";
            var encodedInit = Encoding.UTF8.GetBytes(initData);
            await _webSocket.SendAsync(new ArraySegment<byte>(encodedInit), WebSocketMessageType.Text, true, CancellationToken.None);

            string requestId = "2";
            if (subscribeAdventure)
            {
                string subscription = "{\"id\":\"1\",\"type\":\"start\",\"payload\":{\"variables\":{\"publicId\":\"" + request.Payload.Variables.Input.PublicId + "\"},\"extensions\":{},\"operationName\":null,\"query\":\"subscription ($publicId: String) {\n  subscribeAdventure(publicId: $publicId) {\n    id\n    ...AdventureControllerFragment\n    ...AudioPlayerFragment\n    ...PromptReviewFragment\n    __typename\n  }\n}\n\nfragment AdventureControllerFragment on Adventure {\n  id\n  actionLoading\n  error\n  gameState\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...ActionControllerFragment\n  ...AlterControllerFragment\n  ...QuestControllerFragment\n  ...RememberControllerFragment\n  ...SafetyControllerFragment\n  ...ShareControllerFragment\n  __typename\n}\n\nfragment ActionControllerFragment on Adventure {\n  id\n  publicId\n  actionCount\n  choices\n  error\n  mode\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...DeathControllerFragment\n  __typename\n}\n\nfragment DeathControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  died\n  __typename\n}\n\nfragment AlterControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment QuestControllerFragment on Adventure {\n  id\n  actions {\n    id\n    text\n    __typename\n  }\n  quests {\n    id\n    text\n    completed\n    active\n    actionGainedId\n    actionCompletedId\n    __typename\n  }\n  __typename\n}\n\nfragment RememberControllerFragment on Adventure {\n  id\n  memory\n  __typename\n}\n\nfragment SafetyControllerFragment on Adventure {\n  id\n  hasBannedWord\n  hasUserBannedWord\n  __typename\n}\n\nfragment ShareControllerFragment on Adventure {\n  id\n  userId\n  thirdPerson\n  playPublicId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  __typename\n}\n\nfragment AudioPlayerFragment on Adventure {\n  id\n  music\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment PromptReviewFragment on Adventure {\n  id\n  actionCount\n  __typename\n}\n\",\"auth\":{\"token\":\"hello\"}}}";
#if DEBUG
                Console.WriteLine("subscribing to adventure...");
                Console.WriteLine($"send: {subscription}");
#endif
                var encodedSub = Encoding.UTF8.GetBytes(subscription);
                await _webSocket.SendAsync(new ArraySegment<byte>(encodedSub), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                requestId = "1";
                request.Id = requestId;
            }

            string payload = JsonConvert.SerializeObject(request);
#if DEBUG
            Console.WriteLine($"send: {payload}");
#endif
            var encodedPayload = Encoding.UTF8.GetBytes(payload);
            await _webSocket.SendAsync(new ArraySegment<byte>(encodedPayload), WebSocketMessageType.Text, true, CancellationToken.None);

            while (true)
            {
                using (var ms = new MemoryStream())
                {
                    var buffer = new ArraySegment<byte>(new byte[8192]);
                    WebSocketReceiveResult result = null;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string response = Encoding.UTF8.GetString(ms.ToArray());
#if DEBUG
                        Console.WriteLine($"receive: {response}");
#endif
                        if (response.StartsWith("{\"type\":\"data\",", StringComparison.OrdinalIgnoreCase)) // || response.StartsWith("{\"type\":\"connection_error\"", StringComparison.OrdinalIgnoreCase))
                        {
                            await Task.Delay(2000);
                            string stop = "{\"id\":\"" + requestId + "\",\"type\":\"stop\"}";
#if DEBUG
                            Console.WriteLine($"send: {stop}");
#endif
                            var encodedStop = Encoding.UTF8.GetBytes(stop);
                            await _webSocket.SendAsync(new ArraySegment<byte>(encodedStop), WebSocketMessageType.Text, true, CancellationToken.None);

                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            _webSocket.Dispose();
                            return JsonConvert.DeserializeObject<WebSocketResponse>(response);
                        }
                    }
                }
            }
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

        public async Task<ActionResponse> SendActionAsync(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            var request = new ActionRequest(adventureId, action, text, actionId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<ActionResponse>(response);
        }

        public Task<WebSocketResponse> SendWebSocketRequestAsync(string publicId, ActionType action, string text = "", uint actionId = 0)
            => SendWebSocketRequestAsync(new WebSocketRequest(publicId, action, text, actionId), true);

        public async Task<WebSocketResponse> SendWebSocketRequestAsync(WebSocketRequest request, bool subscribeAdventure = false)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                var task = SendWebSocketRequestInternalAsync(request, subscribeAdventure);

                var completedTask = await Task.WhenAny(task, Task.Delay(30000, tokenSource.Token));
                if (completedTask == task)
                {
                    tokenSource.Cancel();
                    return await task;
                }
                else
                {
                    //_webSocket.Abort();
                    throw new TimeoutException("Timeout");
                }
            }
        }

        public async Task<DeleteResponse> DeleteAdventureAsync(uint adventureId)
        {
            var request = new DeleteRequest(adventureId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<DeleteResponse>(response);
        }
    }
}