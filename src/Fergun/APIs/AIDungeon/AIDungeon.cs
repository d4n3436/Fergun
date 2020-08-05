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
        private static readonly HttpClient _aidClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        public string Token { get; set; }

        public AIDungeon()
        {
        }

        public AIDungeon(string token)
        {
            Token = token;
        }

        private async Task<WebSocketActionResponse> SendWebSocketActionAsync(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            if (string.IsNullOrEmpty(Token))
            {
                throw new NullReferenceException("Token can't be empty.");
            }

            // I've tried to reuse the websocket but for some reason, half of the times the response containing the history list don't have the generated text, idk why
            //await StartWebSocketAsync();

            string subscription = "{\"id\":\"1\",\"type\":\"start\",\"payload\":{\"variables\":{\"id\":\"adventure:" + adventureId.ToString() + "\"},\"extensions\":{},\"operationName\":\"subscribeContent\",\"query\":\"subscription subscribeContent($id: String) {  subscribeContent(id: $id) {    id    historyList    quests    error    memory    mode    actionLoading    characters {      id      userId      name      __typename    }    gameState    thirdPerson    __typename  }}\",\"auth\":{\"token\":\"hello\"}}}";
#if DEBUG
            Console.WriteLine($"send: {subscription}");
#endif
            var encodedSub = Encoding.UTF8.GetBytes(subscription);

            ClientWebSocket _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol("graphql-ws");

            await _webSocket.ConnectAsync(new Uri(WebSocketEndpoint), CancellationToken.None);

            string initData = "{\"type\":\"connection_init\",\"payload\":{\"token\":\"" + Token + "\"}}";
            var encodedInit = Encoding.UTF8.GetBytes(initData);
            await _webSocket.SendAsync(new ArraySegment<byte>(encodedInit), WebSocketMessageType.Text, true, CancellationToken.None);

            await _webSocket.SendAsync(new ArraySegment<byte>(encodedSub), WebSocketMessageType.Text, true, CancellationToken.None);

            var request = new WebSocketActionRequest(adventureId, action, text, actionId);
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

                        if (response.Contains("subscribeContent", StringComparison.OrdinalIgnoreCase))
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            _webSocket.Dispose();
                            return JsonConvert.DeserializeObject<WebSocketActionResponse>(response);
                        }
#if DEBUG
                        // do not send all the history list
                        Console.WriteLine($"receive: {response}");
#endif
                        // idk if this works
                        if (response.Contains("{\"type\":\"complete\"", StringComparison.OrdinalIgnoreCase))
                        {
                            string stop = "{\"id\":\"2\",\"type\":\"stop\"}";
#if DEBUG
                            Console.WriteLine($"send: {stop}");
#endif
                            var encodedStop = Encoding.UTF8.GetBytes(stop);
                            await _webSocket.SendAsync(new ArraySegment<byte>(encodedStop), WebSocketMessageType.Text, true, CancellationToken.None);
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

        public async Task<ActionResponse> RunActionAsync(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            var request = new ActionRequest(adventureId, action, text, actionId);
            string response = await SendApiRequestAsync(request);
            return JsonConvert.DeserializeObject<ActionResponse>(response);
        }

        public async Task<WebSocketActionResponse> RunWebSocketActionAsync(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                var task = SendWebSocketActionAsync(adventureId, action, text, actionId);

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