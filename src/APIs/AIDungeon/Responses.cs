using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class WebSocketResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("payload", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketResponsePayload Payload { get; set; }
    }

    public class WebSocketResponsePayload
    {
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketData Data { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)] // error
        public string Message { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; } // other error list??
    }

    public class WebSocketData
    {
        //[JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
        //public User User { get; set; }

        [JsonProperty("subscribeAdventure", NullValueHandling = NullValueHandling.Ignore)] // subscribe adventure response
        public WebSocketAdventure SubscribeAdventure { get; set; }

        [JsonProperty("scenario", NullValueHandling = NullValueHandling.Ignore)] // get scenario(s) response
        public WebSocketScenario Scenario { get; set; }

        [JsonProperty("addAdventure", NullValueHandling = NullValueHandling.Ignore)] // create adventure response
        public WebSocketAdventure AddAdventure { get; set; }

        [JsonProperty("adventure", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketAdventure Adventure { get; set; } // get adventure response

        [JsonProperty("addAction", NullValueHandling = NullValueHandling.Ignore)] // new action response
        public WebSocketAction AddAction { get; set; }

        [JsonProperty("editAction", NullValueHandling = NullValueHandling.Ignore)] // edit action response
        public WebSocketAction EditAction { get; set; } // alter response

        [JsonProperty("playerAction", NullValueHandling = NullValueHandling.Ignore)] // action response
        public WebSocketAdventure PlayerAction { get; set; }

        [JsonProperty("updateAdventureMemory", NullValueHandling = NullValueHandling.Ignore)]
        public UpdateAdventureMemory UpdateAdventureMemory { get; set; } // remember response

        [JsonProperty("deleteAdventure", NullValueHandling = NullValueHandling.Ignore)] // create adventure response
        public DeleteAdventure DeleteAdventure { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; } // ??
    }

    public class WebSocketScenario
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("contentType", NullValueHandling = NullValueHandling.Ignore)]
        public string ContentType { get; set; }

        [JsonProperty("contentId", NullValueHandling = NullValueHandling.Ignore)]
        public string ContentId { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("memory", NullValueHandling = NullValueHandling.Ignore)]
        public object Memory { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Tags { get; set; }

        [JsonProperty("nsfw", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Nsfw { get; set; }

        [JsonProperty("published", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Published { get; set; }

        [JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("deletedAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("options")]
        public List<ScenarioOption> Options { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class ScenarioOption
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("publicId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PublicId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class WebSocketAdventure
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("playPublicId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PlayPublicId { get; set; }

        [JsonProperty("publicId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PublicId { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("musicTheme", NullValueHandling = NullValueHandling.Ignore)]
        public string MusicTheme { get; set; }

        [JsonProperty("music", NullValueHandling = NullValueHandling.Ignore)]
        public object Music { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Tags { get; set; }

        [JsonProperty("nsfw", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Nsfw { get; set; }

        [JsonProperty("published", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Published { get; set; }

        [JsonProperty("actions", NullValueHandling = NullValueHandling.Ignore)]
        public List<Action> Actions { get; set; }

        [JsonProperty("quests", NullValueHandling = NullValueHandling.Ignore)]
        public List<QuestData> Quests { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketError Error { get; set; }

        [JsonProperty("memory", NullValueHandling = NullValueHandling.Ignore)]
        public string Memory { get; set; }

        [JsonProperty("hasBannedWord", NullValueHandling = NullValueHandling.Ignore)]
        public object HasBannedWord { get; set; }

        [JsonProperty("hasUserBannedWord", NullValueHandling = NullValueHandling.Ignore)]
        public object HasUserBannedWord { get; set; }

        [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
        public string Mode { get; set; }

        [JsonProperty("actionLoading", NullValueHandling = NullValueHandling.Ignore)]
        public bool ActionLoading { get; set; }

        [JsonProperty("characters", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Characters { get; set; }

        [JsonProperty("actionCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? ActionCount { get; set; }

        [JsonProperty("choices", NullValueHandling = NullValueHandling.Ignore)]
        public object Choices { get; set; }

        [JsonProperty("gameState", NullValueHandling = NullValueHandling.Ignore)]
        public string GameState { get; set; }

        [JsonProperty("userId", NullValueHandling = NullValueHandling.Ignore)]
        public string UserId { get; set; }

        [JsonProperty("thirdPerson", NullValueHandling = NullValueHandling.Ignore)]
        public bool ThirdPerson { get; set; }

        [JsonProperty("died", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Died { get; set; }

        [JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("deletedAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class WebSocketAction
    {
        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore)]
        public long? Time { get; set; }

        [JsonProperty("__typename", NullValueHandling = NullValueHandling.Ignore)]
        public string Typename { get; set; }
    }

    public class Action
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class UpdateAdventureMemory
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("memory")]
        public string Memory { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class DeleteAdventure
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("deletedAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("publicId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PublicId { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class WebSocketError
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("time")]
        public int Time { get; set; }
    }

    public class History
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("adventureId")]
        public string AdventureId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("characterName")]
        public string CharacterName { get; set; }

        [JsonProperty("previousActionId")]
        public string PreviousActionId { get; set; }

        [JsonProperty("revertedActionId")]
        public string RevertedActionId { get; set; }

        [JsonProperty("questData")]
        public QuestData QuestData { get; set; } // this can be null

        [JsonProperty("undone", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Undone { get; set; }

        [JsonProperty("died", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Died { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("undoneAt")]
        public DateTimeOffset? UndoneAt { get; set; }
    }

    public class QuestData
    {
        [JsonProperty("quest")]
        public string Quest { get; set; }

        [JsonProperty("completed", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Completed { get; set; }
    }

    public class ErrorInfo
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("locations")]
        public List<Location> Locations { get; set; }

        [JsonProperty("path")]
        public List<string> Path { get; set; }

        [JsonProperty("extensions")]
        public Extension Extension { get; set; }
    }

    public class Extension
    {
        [JsonProperty("code")]
        public string Code { get; set; }
    }

    public class Location
    {
        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("column")]
        public int Column { get; set; }
    }
}