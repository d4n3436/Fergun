using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class WebSocketRequest
    {
        public WebSocketRequest(string id, RequestType requestType, string prompt = null)
        {
            Payload = new WebSocketPayload();
            if (requestType == RequestType.CreateAdventure)
            {
                Payload.Variables = new PayloadVariables
                {
                    ScenarioId = id,
                    Prompt = prompt
                };
                Payload.Query = "mutation ($scenarioId: String, $prompt: String) {\n  addAdventure(scenarioId: $scenarioId, prompt: $prompt) {\n    id\n    publicId\n    title\n    description\n    musicTheme\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    publicId\n    __typename\n  }\n}\n";
            }
            else if (requestType == RequestType.GetScenario)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "query ($publicId: String) {\n  scenario(publicId: $publicId) {\n    ...SelectOptionScenarioFragment\n    __typename\n  }\n}\n\nfragment SelectOptionScenarioFragment on Scenario {\n  id\n  prompt\n  options {\n    id\n    publicId\n    title\n    __typename\n  }\n  __typename\n}\n";
            }
            else if (requestType == RequestType.GetAdventure)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "query ($publicId: String) {\n  adventure(publicId: $publicId) {\n    id\n    userId\n    userJoined\n    publicId\n    actions {\n      id\n      text\n      __typename\n    }\n    ...ContentHeadingFragment\n    __typename\n  }\n}\n\nfragment ContentHeadingFragment on Searchable {\n  id\n  title\n  description\n  tags\n  published\n  publicId\n  actionCount\n  createdAt\n  updatedAt\n  deletedAt\n  user {\n    id\n    username\n    hasPremium\n    avatar\n    isDeveloper\n    __typename\n  }\n  ...VoteButtonFragment\n  ...CommentButtonFragment\n  __typename\n}\n\nfragment VoteButtonFragment on Votable {\n  id\n  userVote\n  totalUpvotes\n  __typename\n}\n\nfragment CommentButtonFragment on Commentable {\n  id\n  publicId\n  allowComments\n  totalComments\n  __typename\n}\n";
            }
            else if (requestType == RequestType.DeleteAdventure)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "mutation ($publicId: String) {\n  deleteAdventure(publicId: $publicId) {\n    id\n    publicId\n    deletedAt\n    __typename\n  }\n}\n";
            }
        }

        public WebSocketRequest(string publicId, ActionType action, string text = "", uint actionId = 0)
        {
            var inputData = new InputData
            {
                PublicId = publicId
            };

            if (!string.IsNullOrEmpty(text) &&
                action != ActionType.Continue &&
                action != ActionType.Undo &&
                action != ActionType.Redo &&
                action != ActionType.Retry)
            {
                inputData.Text = text;
            }

            string query;
            // Alter is weird
            if (actionId != 0 && action == ActionType.Alter)
            {
                inputData.ActionId = actionId.ToString();
                query = "mutation ($input: AlterInput) {\n  editAction(input: $input) {\n    time\n    message\n    __typename\n  }\n}\n";
            }
            else
            {
                inputData.Type = action.ToString().ToLowerInvariant();
                inputData.ChoicesMode = false;
                query = "mutation ($input: ActionInput) {\n  addAction(input: $input) {\n    message\n    time\n    __typename\n  }\n}\n";
            }

            Payload = new WebSocketPayload
            {
                Variables = new PayloadVariables
                {
                    Input = inputData
                },
                Query = query
            };
        }

        [JsonProperty("id")]
        public string Id { get; set; } = "2";

        [JsonProperty("type")]
        public string Type { get; set; } = "start";

        [JsonProperty("payload")]
        public WebSocketPayload Payload { get; set; }
    }

    public class WebSocketPayload
    {
        [JsonProperty("variables")]
        public PayloadVariables Variables { get; set; }

        [JsonProperty("extensions")]
        public ActionExtensions Extensions { get; set; } = new ActionExtensions();

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("auth")]
        public ActionAuth Auth { get; set; } = new ActionAuth();
    }

    public class PayloadVariables
    {
        [JsonProperty("publicId", NullValueHandling = NullValueHandling.Ignore)]
        public string PublicId { get; set; }

        [JsonProperty("scenarioId", NullValueHandling = NullValueHandling.Ignore)]
        public string ScenarioId { get; set; }

        [JsonProperty("input", NullValueHandling = NullValueHandling.Ignore)]
        public InputData Input { get; set; }

        [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string Prompt { get; set; }
    }

    public class InputData
    {
        [JsonProperty("publicId")]
        public string PublicId { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("characterName", NullValueHandling = NullValueHandling.Ignore)]
        public string CharacterName { get; set; }

        [JsonProperty("choicesMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ChoicesMode { get; set; }

        [JsonProperty("memory", NullValueHandling = NullValueHandling.Ignore)]
        public string Memory { get; set; }

        [JsonProperty("actionId", NullValueHandling = NullValueHandling.Ignore)]
        public string ActionId { get; set; }
    }

    public class ActionAuth
    {
        [JsonProperty("token")]
        public string Token { get; set; } = "hello";
    }

    public class ActionExtensions
    {
    }
}