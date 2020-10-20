using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class AidAPI
    {
        public const string WebSocketEndpoint = "wss://api.aidungeon.io/subscriptions";
        public const string AllScenariosId = "edd5fdc0-9c81-11ea-a76c-177e6c0711b5";

        public string Token { get; set; }

        public AidAPI()
        {
        }

        public AidAPI(string token)
        {
            Token = token;
        }

        private async Task<WebSocketResponse> SendWebSocketRequestInternalAsync(WebSocketRequest request, bool subscribeAdventure)
        {
            if (string.IsNullOrEmpty(Token))
            {
                throw new NullReferenceException("Token can't be empty.");
            }

            ClientWebSocket webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol("graphql-ws");

            await webSocket.ConnectAsync(new Uri(WebSocketEndpoint), CancellationToken.None);

            string initData = "{\"type\":\"connection_init\",\"payload\":{\"token\":\"" + Token + "\"}}";
#if DEBUG
            Console.WriteLine("sending connection_init");
#endif
            await webSocket.SendAsync(GetArraySegment(initData), WebSocketMessageType.Text, true, CancellationToken.None);

            string requestId = "2";
            if (subscribeAdventure)
            {
                string subscription = "{\"id\":\"1\",\"type\":\"start\",\"payload\":{\"variables\":{\"publicId\":\"" + request.Payload.Variables.Input.PublicId + "\"},\"extensions\":{},\"operationName\":null,\"query\":\"subscription ($publicId: String) {\n  subscribeAdventure(publicId: $publicId) {\n    id\n    ...AdventureControllerFragment\n    ...AudioPlayerFragment\n    ...PromptReviewFragment\n    __typename\n  }\n}\n\nfragment AdventureControllerFragment on Adventure {\n  id\n  actionLoading\n  error\n  gameState\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...ActionControllerFragment\n  ...AlterControllerFragment\n  ...QuestControllerFragment\n  ...RememberControllerFragment\n  ...SafetyControllerFragment\n  ...ShareControllerFragment\n  __typename\n}\n\nfragment ActionControllerFragment on Adventure {\n  id\n  publicId\n  actionCount\n  choices\n  error\n  mode\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...DeathControllerFragment\n  __typename\n}\n\nfragment DeathControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  died\n  __typename\n}\n\nfragment AlterControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment QuestControllerFragment on Adventure {\n  id\n  actions {\n    id\n    text\n    __typename\n  }\n  quests {\n    id\n    text\n    completed\n    active\n    actionGainedId\n    actionCompletedId\n    __typename\n  }\n  __typename\n}\n\nfragment RememberControllerFragment on Adventure {\n  id\n  memory\n  __typename\n}\n\nfragment SafetyControllerFragment on Adventure {\n  id\n  hasBannedWord\n  hasUserBannedWord\n  __typename\n}\n\nfragment ShareControllerFragment on Adventure {\n  id\n  userId\n  thirdPerson\n  playPublicId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  __typename\n}\n\nfragment AudioPlayerFragment on Adventure {\n  id\n  music\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment PromptReviewFragment on Adventure {\n  id\n  actionCount\n  __typename\n}\n\",\"auth\":{\"token\":\"hello\"}}}";
#if DEBUG
                Console.WriteLine("subscribing to adventure...");
                Console.WriteLine($"send: {subscription}");
#endif
                await webSocket.SendAsync(GetArraySegment(subscription), WebSocketMessageType.Text, true, CancellationToken.None);
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
            await webSocket.SendAsync(GetArraySegment(payload), WebSocketMessageType.Text, true, CancellationToken.None);

            while (true)
            {
                string response;
                using (var ms = new MemoryStream())
                {
                    var buffer = new ArraySegment<byte>(new byte[8192]);
                    WebSocketReceiveResult result = null;
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    response = Encoding.UTF8.GetString(ms.ToArray());
                }
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
                    await webSocket.SendAsync(GetArraySegment(stop), WebSocketMessageType.Text, true, CancellationToken.None);

                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    webSocket.Dispose();
                    return JsonConvert.DeserializeObject<WebSocketResponse>(response);
                }
            }
        }

        public async Task<WebSocketResponse> SendWebSocketRequestAsync(string publicId, ActionType action, string text = "", uint actionId = 0)
        {
            var response = await SendWebSocketRequestAsync(new WebSocketRequest(publicId, action, text, actionId), true);
            // some checks for errors
            if (response == null || response.Payload?.Errors != null || response.Payload?.Data?.Errors != null ||
                response.Payload?.Data?.AddAction != null || response.Payload?.Data?.EditAction?.Message != null)
            {
                return response;
            }
            // Now the websocket doesn't return the adventure after sending an action request,
            // so I'm gonna send an adventure request instead because the other way (keep an websocket alive and wait for the response)
            // is too hard to make (at least to me)
            return await SendWebSocketRequestAsync(new WebSocketRequest(publicId, RequestType.GetAdventure));
        }

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

        private static ArraySegment<byte> GetArraySegment(string text) => new ArraySegment<byte>(Encoding.UTF8.GetBytes(text));
    }
}