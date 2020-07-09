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
            var Vars = new CreationVariables
            {
                Id = $"scenario:{scenarioId}"
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
        public CreationVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($id: String!, $prompt: String) {\n  createAdventureFromScenarioId(id: $id, prompt: $prompt) {\n    id\n    contentType\n    contentId\n    title\n    description\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    publicId\n    historyList\n    __typename\n  }\n}\n";
    }

    public class CreationVariables
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string Prompt { get; set; }
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

    public partial class AdventureListInputRequest
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
                Id = $"adventure:{adventureId}",
                Type = action.ToString().ToLowerInvariant()
            };

            if (!string.IsNullOrEmpty(text))
            {
                if (action != ActionType.Continue && action != ActionType.Undo && action != ActionType.Redo && action != ActionType.Retry)
                {
                    inputData.Text = text;
                }
            }

            if (actionId != 0)
            {
                if (action == ActionType.Alter)
                {
                    inputData.ActionId = actionId.ToString();

                    // Alter command is special :)
                    Query = "mutation ($input: ContentActionInput) {\n  doAlterAction(input: $input) {\n    id\n    historyList\n    __typename\n  }\n}\n";
                }
            }
            Variables = new ActionVariables
            {
                Input = inputData
            };
        }

        [JsonProperty("operationName")]
        public string OperationName { get; set; } = null;

        [JsonProperty("variables")]
        public ActionVariables Variables { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; } = "mutation ($input: ContentActionInput) {\n  doContentAction(input: $input)\n}\n";
    }

    public partial class ActionVariables
    {
        [JsonProperty("input")]
        public InputData Input { get; set; }
    }

    public partial class InputData
    {
        [JsonProperty("actionId", NullValueHandling = NullValueHandling.Ignore)]
        public string ActionId { get; set; }

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }


        [JsonProperty("id")]
        public string Id { get; set; }
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