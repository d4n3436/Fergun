#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fergun.APIs.AIDungeon
{
    // AI Dungeon GraphQL API wrapper
    public class AiDungeonApi
    {
        private readonly HttpClient _httpClient;
        private readonly string? _token;
        public const string ApiEndpoint = "https://api.aidungeon.io/graphql";
        public const string AllScenariosId = "edd5fdc0-9c81-11ea-a76c-177e6c0711b5";
        private static readonly JsonSerializerOptions _defaultSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        public AiDungeonApi() : this(new HttpClient())
        {
        }

        public AiDungeonApi(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public AiDungeonApi(string token) : this(new HttpClient(), token)
        {
        }

        public AiDungeonApi(HttpClient httpClient, string token) : this(httpClient)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        private async Task<Stream> SendRequestAsync<TVariables>(IAiDungeonRequest<TVariables> request, bool requireToken = true)
            => await SendRequestAsync(JsonSerializer.Serialize(request, _defaultSerializerOptions), requireToken);

        private async Task<Stream> SendRequestAsync(string json, bool requireToken = true)
        {
            EnsureToken(_token, requireToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApiEndpoint));
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("X-Access-Token", _token);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<AiDungeonAccount> CreateAnonymousAccountAsync()
        {
            var request = new AiDungeonAnonymousAccountRequest();
            var response = await SendRequestAsync(request, false);

            return DeserializeToEntity<AiDungeonAccount>(response, "createAnonymousAccount");
        }

        public async Task<AiDungeonUser> GetAccountInfoAsync()
        {
            var request = new AiDungeonAccountInfoRequest();
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonUser>(response, "user");
        }

        public async Task<AiDungeonUser> DisableSafeModeAsync(string id)
        {
            var request = new AiDungeonAccountGameSettingsRequest(id, true);
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonUser>(response, "saveGameSettings");
        }

        public async Task<AiDungeonScenario> GetScenarioAsync(string scenarioId)
        {
            var request = new AiDungeonRequest(scenarioId, RequestType.GetScenario);
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonScenario>(response, "scenario");
        }

        // aka add adventure
        public async Task<AiDungeonAdventure> CreateAdventureAsync(string scenarioId, string? prompt = null)
        {
            var request = new AiDungeonRequest(scenarioId, RequestType.CreateAdventure, prompt);
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonAdventure>(response, "addAdventure");
        }

        public async Task<AiDungeonAdventure> GetAdventureAsync(string publicId)
        {
            var request = new AiDungeonRequest(publicId, RequestType.GetAdventure);
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonAdventure>(response, "adventure");
        }

        // aka add action
        public async Task<AiDungeonAdventure> SendActionAsync(string publicId, ActionType type, string? text = null, long actionId = 0)
        {
            var request = new AiDungeonRequest(publicId, type, text, actionId);
            await SendRequestAsync(request);

            return await GetAdventureAsync(publicId);
        }

        public async Task<AiDungeonAdventure> DeleteAdventureAsync(string publicId)
        {
            var request = new AiDungeonRequest(publicId, RequestType.DeleteAdventure);
            var response = await SendRequestAsync(request);

            return DeserializeToEntity<AiDungeonAdventure>(response, "deleteAdventure");
        }

        private static TEntity DeserializeToEntity<TEntity>(Stream stream, string propertyName) where TEntity : IAiDungeonEntity
        {
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                using var enumerator = errors.EnumerateArray();
                if (enumerator.MoveNext() && enumerator.Current.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    throw new AiDungeonException(message.GetString());
                }
            }

            return root
                .GetProperty("data")
                .GetProperty(propertyName)
                .Deserialize<TEntity>(_defaultSerializerOptions) ?? throw new AiDungeonException("Failed to deserialize the response data.");
        }

        private static void EnsureToken(string? token, bool requireToken)
        {
            if (requireToken && token == null)
            {
                throw new InvalidOperationException("Cannot send this request because requireToken is true and token is null.");
            }
        }
    }
}