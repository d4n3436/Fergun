using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    // TODO: Use GraphQL query builder

    public class RegisterRequest
    {
        public RegisterRequest(string email, string username, string password)
        {
            Variables = new RegisterVariables
            {
                Email = email,
                Username = username,
                Password = password
            };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public RegisterVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($email: String!, $username: String!, $password: String!) {\n  createAccount(email: $email, username: $username, password: $password) {\n    id\n    accessToken\n    __typename\n  }\n}\n";
    }

    public class RegisterVariables
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    public class AnonymousAccountRequest
    {
        public AnonymousAccountRequest()
        {
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public AnonymousAccountVariables Variables { get; set; } = new AnonymousAccountVariables();

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation {\n  createAnonymousAccount {\n    id\n    accessToken\n    __typename\n  }\n}\n";
    }

    public class AnonymousAccountVariables
    {
    }

    public class LoginRequest
    {
        public LoginRequest(string email, string password)
        {
            Variables = new LoginVariables
            {
                Email = email,
                Password = password
            };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public LoginVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($email: String!, $password: String!) {\n  login(email: $email, password: $password) {\n    id\n    accessToken\n    __typename\n  }\n}\n";
    }

    public class LoginVariables
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public ForgotPasswordRequest(string email)
        {
            Variables = new ForgotPasswordVariables { Email = email };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public ForgotPasswordVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($email: String!) {\n  sendForgotPasswordEmail(email: $email)\n}\n";
    }

    public class ForgotPasswordVariables
    {
        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class ScenarioRequest
    {
        public ScenarioRequest(uint scenarioId)
        {
            Variables = new ScenarioVariables { Id = $"scenario:{scenarioId}" };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public ScenarioVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "query ($id: String) {\n  content(id: $id) {\n    id\n    contentType\n    contentId\n    title\n    description\n    prompt\n    memory\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    options {\n      id\n      title\n      __typename\n    }\n    __typename\n  }\n}\n";
    }

    public class ScenarioVariables
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class CreationRequest
    {
        public CreationRequest(uint scenarioId, string prompt = null)
        {
            var Vars = new PayloadVariables
            {
                ScenarioId = $"scenario:{scenarioId}"
            };
            if (prompt != null)
            {
                Vars.Prompt = prompt;
            }

            Variables = Vars;
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public PayloadVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($id: String!, $prompt: String) {\n  createAdventureFromScenarioId(id: $id, prompt: $prompt) {\n    id\n    contentType\n    contentId\n    title\n    description\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    publicId\n    historyList\n    __typename\n  }\n}\n";
    }

    public class AdventureListRequest
    {
        public AdventureListRequest()
        {
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = "user";

        [JsonProperty("variables")]
        public AdventureListVariables Variables { get; set; } = new AdventureListVariables();

        [JsonProperty("query")]
        public string Query { get; set; } = "query user($input: ContentListInput) {\n  user {\n    id\n    contentList(input: $input) {\n      id\n      contentType\n      contentId\n      title\n      description\n      tags\n      nsfw\n      published\n      createdAt\n      updatedAt\n      deletedAt\n      username\n      userVote\n      totalUpvotes\n      totalDownvotes\n      totalComments\n      __typename\n    }\n    __typename\n  }\n}\n";
    }

    public class AdventureListVariables
    {
        [JsonProperty("input")]
        public AdventureListInputRequest Input { get; set; } = new AdventureListInputRequest();
    }

    public class AdventureListInputRequest
    {
        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "adventure";

        [JsonProperty("searchTerm")]
        public string SearchTerm { get; set; } = "";

        [JsonProperty("thirdPerson")]
        public string ThirdPerson { get; set; } = null;

        [JsonProperty("sortOrder")]
        public string SortOrder { get; set; } = "createdAt";

        [JsonProperty("timeRange")]
        public string TimeRange { get; set; } = null;
    }

    public class RefreshRequest
    {
        public RefreshRequest()
        {
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public RefreshVariables Variables { get; set; } = new RefreshVariables();

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation {\n  refreshSearchIndex\n}\n";
    }

    public class RefreshVariables
    {
    }

    public class AdventureInfoRequest
    {
        public AdventureInfoRequest(uint adventureId)
        {
            Variables = new AdventureVariables
            {
                Id = $"adventure:{adventureId}"
            };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public AdventureVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "query ($id: String) {\n  content(id: $id) {\n    id\n    published\n    createdAt\n    historyList\n    weeklyContest\n    __typename\n  }\n}\n";
    }

    public class AdventureVariables
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class ActionRequest
    {
        public ActionRequest(uint adventureId, ActionType action, string text = "", uint actionId = 0)
        {
            var inputData = new InputData
            {
                PublicId = $"adventure:{adventureId}",
                Type = action.ToString().ToLowerInvariant()
            };

            if (!string.IsNullOrEmpty(text) &&
                action != ActionType.Continue &&
                action != ActionType.Undo &&
                action != ActionType.Redo &&
                action != ActionType.Retry)
            {
                inputData.Text = text;
            }

            if (actionId != 0 && action == ActionType.Alter)
            {
                inputData.ActionId = actionId.ToString();

                // Alter command is special :)
                Query = "mutation ($input: ContentActionInput) {\n  doAlterAction(input: $input) {\n    id\n    historyList\n    __typename\n  }\n}\n";
            }
            Variables = new PayloadVariables
            {
                Input = inputData
            };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public PayloadVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($input: ContentActionInput) {\n  doContentAction(input: $input)\n}\n";
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

        [JsonProperty("choicesMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ChoicesMode { get; set; }

        [JsonProperty("memory", NullValueHandling = NullValueHandling.Ignore)]
        public string Memory { get; set; }

        [JsonProperty("actionId", NullValueHandling = NullValueHandling.Ignore)]
        public string ActionId { get; set; }
    }

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
                Payload.Query = "mutation ($scenarioId: String, $prompt: String) {\n  createAdventure(scenarioId: $scenarioId, prompt: $prompt) {\n    id\n    publicId\n    title\n    description\n    musicTheme\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    publicId\n    __typename\n  }\n}\n";
            }
            else if (requestType == RequestType.GetScenario)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "query ($publicId: String) {\n  user {\n    ...DisplayFragment\n    __typename\n  }\n  scenario(publicId: $publicId) {\n    ...PlayScenarioFragment\n    __typename\n  }\n}\n\nfragment PlayScenarioFragment on Scenario {\n  id\n  prompt\n  options {\n    id\n    publicId\n    title\n    __typename\n  }\n  __typename\n}\n\nfragment DisplayFragment on User {\n  id\n  gameSettings {\n    id\n    accessibilityMode\n    ...StoryTextFragment\n    __typename\n  }\n  __typename\n}\n\nfragment StoryTextFragment on GameSettings {\n  id\n  textSpeed\n  textSize\n  textFont\n  __typename\n}\n";
            }
            else if (requestType == RequestType.GetAdventure)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "query ($publicId: String) {\n  adventure(publicId: $publicId) {\n    id\n    playPublicId\n    publicId\n    thirdPerson\n    actions {\n      id\n      text\n      __typename\n    }\n    ...AdventureControllerFragment\n    ...AudioPlayerFragment\n    ...PromptReviewFragment\n    __typename\n  }\n}\n\nfragment AdventureControllerFragment on Adventure {\n  id\n  actionLoading\n  error\n  gameState\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...ActionControllerFragment\n  ...AlterControllerFragment\n  ...QuestControllerFragment\n  ...RememberControllerFragment\n  ...SafetyControllerFragment\n  ...ShareControllerFragment\n  __typename\n}\n\nfragment ActionControllerFragment on Adventure {\n  id\n  publicId\n  actionCount\n  choices\n  error\n  mode\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...DeathControllerFragment\n  __typename\n}\n\nfragment DeathControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  died\n  __typename\n}\n\nfragment AlterControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment QuestControllerFragment on Adventure {\n  id\n  actions {\n    id\n    text\n    __typename\n  }\n  quests {\n    id\n    text\n    completed\n    active\n    actionGainedId\n    actionCompletedId\n    __typename\n  }\n  __typename\n}\n\nfragment RememberControllerFragment on Adventure {\n  id\n  memory\n  __typename\n}\n\nfragment SafetyControllerFragment on Adventure {\n  id\n  hasBannedWord\n  hasUserBannedWord\n  __typename\n}\n\nfragment ShareControllerFragment on Adventure {\n  id\n  userId\n  thirdPerson\n  playPublicId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  __typename\n}\n\nfragment AudioPlayerFragment on Adventure {\n  id\n  music\n  actions {\n    id\n    text\n    __typename\n  }\n  __typename\n}\n\nfragment PromptReviewFragment on Adventure {\n  id\n  actionCount\n  __typename\n}\n";
            }
            else if (requestType == RequestType.DeleteAdventure)
            {
                Payload.Variables = new PayloadVariables
                {
                    PublicId = id
                };
                Payload.Query = "mutation ($publicId: String) {\n  deleteAdventure(publicId: $publicId) {\n    publicId\n    deletedAt\n    __typename\n  }\n}\n";
            }
        }

        public WebSocketRequest(string publicId, ActionType action, string text = "", uint actionId = 0)
        {
            var inputData = new InputData
            {
                PublicId = publicId,
                Type = action.ToString().ToLowerInvariant(),
                ChoicesMode = false
            };

            if (!string.IsNullOrEmpty(text) &&
                action != ActionType.Continue &&
                action != ActionType.Undo &&
                action != ActionType.Redo &&
                action != ActionType.Retry)
            {
                inputData.Text = text;
            }

            if (actionId != 0 && action == ActionType.Alter)
            {
                inputData.ActionId = actionId.ToString();
            }

            Payload = new WebSocketPayload
            {
                Variables = new PayloadVariables
                {
                    Input = inputData
                },
                Query = "mutation ($input: ActionInput) {\n  playerAction(input: $input) {\n    actions {\n      id\n      text\n      __typename\n    }\n    ...ActionControllerFragment\n    __typename\n  }\n}\n\nfragment ActionControllerFragment on Adventure {\n  id\n  publicId\n  actionCount\n  choices\n  error\n  mode\n  thirdPerson\n  userId\n  characters {\n    id\n    userId\n    name\n    __typename\n  }\n  ...DeathControllerFragment\n  __typename\n}\n\nfragment DeathControllerFragment on Adventure {\n  id\n  publicId\n  mode\n  died\n  __typename\n}\n"
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

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("auth")]
        public ActionAuth Auth { get; set; } = new ActionAuth();
    }

    public class ActionAuth
    {
        [JsonProperty("token")]
        public string Token { get; set; } = "hello";
    }

    public class ActionExtensions
    {
    }

    public class DeleteRequest
    {
        public DeleteRequest(uint adventureId)
        {
            Variables = new DeleteVariables { Id = $"adventure:{adventureId}" };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public DeleteVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($id: String!) {\n  deleteContent(id: $id) {\n    id\n    deletedAt\n    __typename\n  }\n}\n";
    }

    public class DeleteVariables
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}