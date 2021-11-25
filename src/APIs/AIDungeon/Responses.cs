#nullable enable
using System;
using System.Collections.Generic;

namespace Fergun.APIs.AIDungeon
{
    public class AiDungeonScenario : IAiDungeonEntity
    {
        public long Id { get; set; }

        public Guid PublicId { get; set; }

        public string? Memory { get; set; }

        public string? Prompt { get; set; }

        public string? Title { get; set; }

        public IReadOnlyList<AiDungeonScenario> Options { get; set; } = Array.Empty<AiDungeonScenario>();
    }

    public class AiDungeonAdventure : IAiDungeonEntity
    {
        public long Id { get; set; }

        public Guid? PublicId { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public bool? Nsfw { get; set; }

        public bool? Published { get; set; }

        public IReadOnlyList<AiDungeonAction> Actions { get; set; } = Array.Empty<AiDungeonAction>();

        public IReadOnlyList<AiDungeonAction> UndoneWindow { get; set; } = Array.Empty<AiDungeonAction>();

        public DateTimeOffset? CreatedAt { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class AiDungeonAction : IAiDungeonEntity
    {
        public long Id { get; set; }

        public string Text { get; set; } = "";

        public DateTimeOffset? UndoneAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }
    }

    public class AiDungeonAccount : IAiDungeonEntity
    {
        public long Id { get; set; }

        public string AccessToken { get; set; } = "";
    }

    public class AiDungeonUser : IAiDungeonEntity
    {
        public long Id { get; set; }

        public string? Username { get; set; }

        public AiDungeonGameSettings GameSettings { get; set; } = new AiDungeonGameSettings();
    }

    // partial data
    public class AiDungeonGameSettings : IAiDungeonEntity
    {
        public long Id { get; set; }

        public bool NsfwGeneration { get; set; }

        public bool UnrestrictedInput { get; set; }

        public string? ModelType { get; set; }
    }
}