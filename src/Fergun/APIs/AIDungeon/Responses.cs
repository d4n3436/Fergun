using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.AIDungeon
{
    public class RegisterResponse : IResponse
    {
        [JsonProperty("data")]
        public RegisterData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class AnonymousAccountResponse : IResponse
    {
        [JsonProperty("data")]
        public RegisterData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class LoginResponse : IResponse
    {
        [JsonProperty("data")]
        public LoginData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class LoginData
    {
        [JsonProperty("login")]
        public AccountInfo Login { get; set; }
    }

    public class AnonymousAccountData
    {
        [JsonProperty("createAnonymousAccount")]
        public AccountInfo CreateAnonymousAccount { get; set; }
    }

    public class RegisterData
    {
        [JsonProperty("createAccount")]
        public AccountInfo CreateAccount { get; set; }
    }

    public class AccountInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class ForgotPasswordResponse : IResponse
    {
        [JsonProperty("data")]
        public ForgotPasswordData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class ForgotPasswordData
    {
        [JsonProperty("sendForgotPasswordEmail")]
        public bool SendForgotPasswordEmail { get; set; }
    }

    public class UserInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("lastPlayedAdventure")]
        public string LastPlayedAdventure { get; set; }

        [JsonProperty("lastAdventureId")]
        public string LastAdventureId { get; set; }

        [JsonProperty("ip")]
        public object Ip { get; set; }

        [JsonProperty("isSetup")]
        public bool IsSetup { get; set; }

        [JsonProperty("isSubscribed")]
        public bool IsSubscribed { get; set; }

        [JsonProperty("isContributor")]
        public bool IsContributor { get; set; }

        [JsonProperty("bannedAt")]
        public DateTimeOffset? BannedAt { get; set; }

        [JsonProperty("verifiedAt")]
        public DateTimeOffset VerifiedAt { get; set; }

        [JsonProperty("gameSafeMode")]
        public bool GameSafeMode { get; set; }

        [JsonProperty("gameProofRead")]
        public bool GameProofRead { get; set; }

        [JsonProperty("gameTemperature")]
        public int GameTemperature { get; set; }

        [JsonProperty("gameTextLength")]
        public int GameTextLength { get; set; }

        [JsonProperty("gameDirectDialog")]
        public bool GameDirectDialog { get; set; }

        [JsonProperty("gameDevModel")]
        public bool GameDevModel { get; set; }

        [JsonProperty("gameShowTips")]
        public bool GameShowTips { get; set; }

        [JsonProperty("gamePlayAudio")]
        public bool GamePlayAudio { get; set; }

        [JsonProperty("gamePlayMusic")]
        public bool GamePlayMusic { get; set; }

        [JsonProperty("gameShowCommands")]
        public bool GameShowCommands { get; set; }

        [JsonProperty("gameNarrationVolume")]
        public int GameNarrationVolume { get; set; }

        [JsonProperty("gameMusicVolume")]
        public int GameMusicVolume { get; set; }

        [JsonProperty("gameVoiceGender")]
        public string GameVoiceGender { get; set; }

        [JsonProperty("gameVoiceAccent")]
        public string GameVoiceAccent { get; set; }

        [JsonProperty("gameTextColor")]
        public string GameTextColor { get; set; }

        [JsonProperty("gameTextSpeed")]
        public int GameTextSpeed { get; set; }

        [JsonProperty("gameSettingsId")]
        public string GameSettingsId { get; set; }

        [JsonProperty("stripeCustomerId")]
        public object StripeCustomerId { get; set; }

        [JsonProperty("deviceTokens")]
        public DeviceTokens DeviceTokens { get; set; }

        [JsonProperty("sawUpdatesAt")]
        public DateTimeOffset SawUpdatesAt { get; set; }

        [JsonProperty("modelTestOptedOut")]
        public object ModelTestOptedOut { get; set; }

        [JsonProperty("lastModelUsed")]
        public string LastModelUsed { get; set; }

        [JsonProperty("reviewedAt")]
        public DateTimeOffset? ReviewedAt { get; set; }

        [JsonProperty("promptedToReviewAt")]
        public DateTimeOffset PromptedToReviewAt { get; set; }

        [JsonProperty("avatar")]
        public object Avatar { get; set; }

        [JsonProperty("isDeveloper")]
        public bool IsDeveloper { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class DeviceTokens
    {
        [JsonProperty("ios")]
        public List<string> Ios { get; set; }

        [JsonProperty("web")]
        public List<string> Web { get; set; }

        [JsonProperty("android")]
        public List<string> Android { get; set; }
    }

    public class AdventureListResponse : IResponse
    {
        [JsonProperty("data")]
        public AdventureListData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class AdventureListData
    {
        [JsonProperty("user")]
        public AdventureListUser User { get; set; }
    }

    public class AdventureListUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("contentList")]
        public List<ContentList> ContentList { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class ContentList
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentId")]
        public string ContentId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("nsfw")]
        public bool Nsfw { get; set; }

        [JsonProperty("published")]
        public bool Published { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("userVote")]
        public string UserVote { get; set; }

        [JsonProperty("totalUpvotes")]
        public int TotalUpvotes { get; set; }

        [JsonProperty("totalDownvotes")]
        public int TotalDownvotes { get; set; }

        [JsonProperty("totalComments")]
        public int TotalComments { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class RefreshResponse : IResponse
    {
        [JsonProperty("data")]
        public RefreshData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class RefreshData
    {
        [JsonProperty("refreshSearchIndex")]
        public int? RefreshSearchIndex { get; set; }
    }

    public class AdventureInfoResponse : IResponse
    {
        [JsonProperty("data")]
        public AdventureInfoData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class AdventureInfoData
    {
        [JsonProperty("content")]
        public AdventureInfoContent Content { get; set; }
    }

    public class AdventureInfoContent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("published")]
        public bool Published { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("historyList")]
        public List<History> HistoryList { get; set; }

        [JsonProperty("weeklyContest")]
        public bool? WeeklyContext { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class ScenarioResponse : IResponse
    {
        [JsonProperty("data")]
        public ScenarioData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class ScenarioData
    {
        [JsonProperty("content")]
        public ScenarioContent Content { get; set; }
    }

    public class ScenarioContent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentId")]
        public string ContentId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("memory")]
        public object Memory { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("nsfw")]
        public bool? Nsfw { get; set; }

        [JsonProperty("published")]
        public bool? Published { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("deletedAt")]
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

        [JsonProperty("publicId")]
        public Guid? PublicId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class CreationResponse : IResponse
    {
        [JsonProperty("data")]
        public AdventureData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class AdventureData
    {
        [JsonProperty("createAdventureFromScenarioId")]
        public AdventureContent AdventureInfo { get; set; }
    }

    public class AdventureContent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("contentId")]
        public string ContentId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("musicTheme")]
        public string MusicTheme { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("nsfw")]
        public bool Nsfw { get; set; }

        [JsonProperty("published")]
        public bool Published { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty("publicId")]
        public Guid PublicId { get; set; }

        [JsonProperty("historyList")]
        public List<History> HistoryList { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public class ActionResponse : IResponse
    {
        [JsonProperty("data")]
        public ActionData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class ActionData
    {
        [JsonProperty("doContentAction", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DoContentAction { get; set; }

        // idk why Alter is the only command that returns this; i'll ignore it
        [JsonProperty("doAlterAction", NullValueHandling = NullValueHandling.Ignore)]
        public object DoAlterAction { get; set; }
    }

    public class WebSocketResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("payload")]
        public WebSocketResponsePayload Payload { get; set; }
    }

    public class WebSocketResponsePayload
    {
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketData Data { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)] // error
        public string Message { get; set; }
    }

    public class WebSocketData
    {
        //[JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
        //public User User { get; set; }

        [JsonProperty("subscribeAdventure", NullValueHandling = NullValueHandling.Ignore)] // subscribe adventure response
        public WebSocketAdventure SubscribeAdventure { get; set; }

        [JsonProperty("scenario", NullValueHandling = NullValueHandling.Ignore)] // get scenario(s) response
        public ScenarioContent Scenario { get; set; }

        [JsonProperty("createAdventure", NullValueHandling = NullValueHandling.Ignore)] // create adventure response
        public WebSocketAdventure CreateAdventure { get; set; }

        [JsonProperty("adventure", NullValueHandling = NullValueHandling.Ignore)]
        public WebSocketAdventure Adventure { get; set; } // get adventure response

        [JsonProperty("playerAction", NullValueHandling = NullValueHandling.Ignore)] // action response
        public WebSocketAdventure PlayerAction { get; set; }

        [JsonProperty("updateAdventureMemory", NullValueHandling = NullValueHandling.Ignore)]
        public UpdateAdventureMemory UpdateAdventureMemory { get; set; } // remember response

        [JsonProperty("alterAction", NullValueHandling = NullValueHandling.Ignore)]
        public AlterAction AlterAction { get; set; } // alter response

        [JsonProperty("deleteAdventure", NullValueHandling = NullValueHandling.Ignore)] // create adventure response
        public DeleteAdventure DeleteAdventure { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; } // ??
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

    public class Action
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
    }

    public partial class AlterAction
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("publicId")]
        public Guid PublicId { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("actions")]
        public List<Action> Actions { get; set; }

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

    public partial class DeleteAdventure
    {
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
        public long Time { get; set; }
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

    public class DeleteResponse : IResponse
    {
        [JsonProperty("data")]
        public DeleteData Data { get; set; }

        [JsonProperty("errors", NullValueHandling = NullValueHandling.Ignore)]
        public List<ErrorInfo> Errors { get; set; }
    }

    public class DeleteData
    {
        [JsonProperty("deleteContent")]
        public DeleteContent DeleteContent { get; set; }
    }

    public class DeleteContent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("deletedAt")]
        public DateTimeOffset DeletedAt { get; set; }

        [JsonProperty("__typename")]
        public string Typename { get; set; }
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

    public interface IResponse
    {
        List<ErrorInfo> Errors { get; set; }
    }
}