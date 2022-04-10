#nullable enable
using System;

namespace Fergun.APIs.AIDungeon
{
    public class AiDungeonRequest : IAiDungeonRequest<AiDungeonPayloadVariables>
    {
        public AiDungeonRequest(string id, RequestType requestType, string? prompt = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Query = requestType switch
            {
                RequestType.CreateAdventure => "mutation ($scenarioId: String, $prompt: String, $memory: String) {\n  addAdventure(scenarioId: $scenarioId, prompt: $prompt, memory: $memory) {\n    id\n    publicId\n    title\n    description\n    tags\n    nsfw\n    published\n    createdAt\n    updatedAt\n    deletedAt\n    publicId\n    __typename\n  }\n}\n",
                RequestType.GetScenario => "query ($publicId: String) {\n  scenario(publicId: $publicId) {\n    memory\n    ...SelectOptionScenario\n    __typename\n  }\n}\n\nfragment SelectOptionScenario on Scenario {\n  id\n  prompt\n  publicId\n  options {\n    id\n    publicId\n    title\n    __typename\n  }\n  __typename\n}\n",
                RequestType.GetAdventure => "query ($publicId: String) {\n  adventure(publicId: $publicId) {\n    id\n    publicId\n    title\n    description\n    nsfw\n    published\n    actions {\n      id\n      text\n      undoneAt\n      deletedAt\n    }\n    undoneWindow {\n      id\n      text\n      undoneAt\n      deletedAt\n    }\n    createdAt\n    updatedAt\n    deletedAt\n  }\n}",
                RequestType.DeleteAdventure => "mutation ($publicId: String) {\n  deleteAdventure(publicId: $publicId) {\n    id\n    publicId\n    deletedAt\n    __typename\n  }\n}\n",
                _ => throw new ArgumentException("Unknown request type.")
            };

            Variables = requestType == RequestType.CreateAdventure
                ? new AiDungeonPayloadVariables { ScenarioId = id, Prompt = prompt }
                : new AiDungeonPayloadVariables { PublicId = id };
        }

        public AiDungeonRequest(string publicId, ActionType action, string? text = null, long actionId = 0)
        {
            if (string.IsNullOrEmpty(publicId))
            {
                throw new ArgumentNullException(nameof(publicId));
            }

            var inputData = new AiDungeonInputData
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
                query = "mutation ($input: ActionInput) {\n  addAction(input: $input) {\n    message\n    time\n    hasBannedWord\n    returnedInput\n    __typename\n  }\n}\n";
            }

            Variables = new AiDungeonPayloadVariables { Input = inputData };
            Query = query;
        }

        public AiDungeonPayloadVariables Variables { get; set; }

        public string Query { get; set; }
    }

    public class AiDungeonAnonymousAccountRequest : IAiDungeonRequest<EmptyPayloadVariables>
    {
        public EmptyPayloadVariables Variables { get; set; } = new EmptyPayloadVariables();

        public string Query { get; set; } = "mutation {\n  createAnonymousAccount {\n    id\n    accessToken\n  }\n}\n";
    }

    public class AiDungeonAccountInfoRequest : IAiDungeonRequest<EmptyPayloadVariables>
    {
        public EmptyPayloadVariables Variables { get; set; } = new EmptyPayloadVariables();

        public string Query { get; set; } = "{\n  user {\n    id\n    username\n    ...ContentListUser\n  }\n}\n\nfragment ContentListUser on User {\n  ...ContentCardUser\n}\n\nfragment ContentCardUser on User {\n  id\n  username\n  gameSettings {\n    id\n    nsfwGeneration\n    unrestrictedInput\n  }\n}\n";
    }

    public class AiDungeonAccountGameSettingsRequest : IAiDungeonRequest<AccountGameSettingsPayloadVariables>
    {
        public AiDungeonAccountGameSettingsRequest(string id, bool nsfwGeneration)
        {
            Variables = new AccountGameSettingsPayloadVariables
            {
                Input = new AccountGameSettingsInput
                {
                    Id = id,
                    NsfwGeneration = nsfwGeneration
                }
            };
        }

        public AccountGameSettingsPayloadVariables Variables { get; set; }

        public string Query { get; set; } = "mutation ($input: GameSettingsInput) {\n  saveGameSettings(input: $input) {\n    id\n    gameSettings {\n      id\n      ...GameSettingsGameSettings\n    }\n  }\n}\n\nfragment GameSettingsGameSettings on GameSettings {\n  modelType\n  nsfwGeneration\n}\n";
    }

    public class AccountGameSettingsPayloadVariables
    {
        public AccountGameSettingsInput Input { get; set; } = new AccountGameSettingsInput();
    }

    public class AccountGameSettingsInput
    {
        public string Id { get; set; } = "";

        public bool NsfwGeneration { get; set; }
    }

    public class EmptyPayloadVariables
    {
    }

    public class AiDungeonPayloadVariables
    {
        public string? PublicId { get; set; }

        public string? ScenarioId { get; set; }

        public AiDungeonInputData? Input { get; set; }

        public string? Prompt { get; set; }
    }

    public class AiDungeonInputData
    {
        public string? PublicId { get; set; }

        public string? Type { get; set; }

        public string? Text { get; set; }

        public string? CharacterName { get; set; }

        public bool? ChoicesMode { get; set; }

        public string? Memory { get; set; }

        public string? ActionId { get; set; }
    }
}