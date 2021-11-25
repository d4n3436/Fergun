using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fergun.APIs.AIDungeon;
using Xunit;

namespace Fergun.Tests
{
    public class AiDungeonFixture
    {
        public string Token { get; set; }

        public string[] InitialPromptWords { get; set; }

        public string NormalAdventureId { get; set; }

        public long LastActionId { get; set; }
    }

    [CollectionDefinition(nameof(AiDungeonTests), DisableParallelization = true)]
    public class AiDungeonTestsCollectionDefinition : ICollectionFixture<AiDungeonFixture>
    {
    }

    // These tests have to run sequentially because first we need to create an anonymous account, then create, use, and delete an adventure, in that order.
    [TestCaseOrderer("Fergun.Tests.PriorityOrderer", "Fergun.Tests")]
    [Collection(nameof(AiDungeonTests))]
    public class AiDungeonTests
    {
        private readonly AiDungeonFixture _fixture;

        public AiDungeonTests(AiDungeonFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [TestPriority(0)]
        public async Task GetAnonymousAccountTest()
        {
            var api = new AiDungeonApi();
            // Act
            var account = await api.CreateAnonymousAccountAsync();
            _fixture.Token = account.AccessToken;

            // Assert
            Assert.NotNull(_fixture.Token);

            api = new AiDungeonApi(_fixture.Token);

            // Get game settings id
            var user = await api.GetAccountInfoAsync();

            string gameId = user.GameSettings.Id.ToString();

            // Disable safe mode
            await api.DisableSafeModeAsync(gameId);
        }

        [Fact]
        [TestPriority(1)]
        public async Task CreateAdventureTest()
        {
            var api = new AiDungeonApi(_fixture.Token);
            var rng = new Random();

            // Get mode list
            var scenario = await api.GetScenarioAsync(AiDungeonApi.AllScenariosId);

            Assert.NotEmpty(scenario.Options);

            var filteredModeList = scenario.Options
                .Where(x => !x.Title?.Contains("custom", StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            string id = filteredModeList[rng.Next(filteredModeList.Count)].PublicId.ToString();

            // Get scenario list from a random mode
            scenario = await api.GetScenarioAsync(id);

            Assert.NotEmpty(scenario.Options);

            var filteredScenarioList = scenario.Options
                .Where(x => !x.Title?.Contains("custom", StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            id = filteredScenarioList[rng.Next(filteredScenarioList.Count)].PublicId.ToString();

            // Get character from a random scenario
            scenario = await api.GetScenarioAsync(id);

            // Create adventure
            var adventure = await api.CreateAdventureAsync(scenario.Id.ToString(), scenario.Prompt?.Replace("${character.name}", "Fergun", StringComparison.OrdinalIgnoreCase));

            string publicId = adventure.PublicId?.ToString();

            Assert.NotNull(publicId);

            // Get adventure
            adventure = await api.GetAdventureAsync(publicId);

            _fixture.NormalAdventureId = adventure.PublicId?.ToString();

            var actions = adventure.Actions.Where(x => !string.IsNullOrEmpty(x.Text)).ToList();

            Assert.NotEmpty(actions);

            // Get initial prompt
            string initialPrompt = actions[^1].Text;
            if (actions.Count > 1)
            {
                actions.RemoveAt(actions.Count - 1);
                initialPrompt = string.Concat(actions.Select(x => x.Text)) + initialPrompt;
            }

            // Get initial prompt words
            _fixture.InitialPromptWords = initialPrompt.Split(' ');
        }

        [Theory]
        [MemberData(nameof(Actions))]
        [TestPriority(2)]
        public async Task PlayAdventureTest(ActionType actionType)
        {
            // Arrange
            if (actionType == ActionType.Alter)
                return;

            var api = new AiDungeonApi(_fixture.Token);
            var rng = new Random();
            string text = "";

            // Generate random text based on the initial prompt
            for (int i = 0; i < 20; i++)
            {
                text += $" {_fixture.InitialPromptWords[rng.Next(_fixture.InitialPromptWords.Length)]}";
            }

            // Act
            var response = await api.SendActionAsync(_fixture.NormalAdventureId, actionType, text);

            // Assert
            Assert.NotEmpty(response.Actions);
        }

        [Fact]
        [TestPriority(3)]
        public async Task GetAdventureTest()
        {
            // Arrange
            var api = new AiDungeonApi(_fixture.Token);

            // Act
            var adventure = await api.GetAdventureAsync(_fixture.NormalAdventureId);

            // Assert
            Assert.NotEmpty(adventure.Actions);

            _fixture.LastActionId = adventure.Actions[^1].Id;
        }

        [Fact]
        [TestPriority(4)]
        public async Task AlterAdventureTest()
        {
            // Arrange
            var api = new AiDungeonApi(_fixture.Token);
            var rng = new Random();
            string text = "";

            // Generate random text based on the initial prompt
            for (int i = 0; i < 20; i++)
            {
                text += $" {_fixture.InitialPromptWords[rng.Next(_fixture.InitialPromptWords.Length)]}";
            }

            // Act
            var adventure = await api.SendActionAsync(_fixture.NormalAdventureId, ActionType.Alter, text, _fixture.LastActionId);

            // Assert
            Assert.NotEmpty(adventure.Actions);
        }

        [Fact]
        [TestPriority(5)]
        public async Task DeleteAdventureTest()
        {
            // Arrange
            var api = new AiDungeonApi(_fixture.Token);

            // Act
            await api.DeleteAdventureAsync(_fixture.NormalAdventureId);
        }

        public static IEnumerable<object[]> Actions()
        {
            foreach (var value in Enum.GetValues(typeof(ActionType)))
            {
                yield return new[] { value };
            }
        }
    }
}