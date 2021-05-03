using System;
using System.Collections.Generic;
using System.Globalization;
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
            // Act
            var response = await AidGraphQlApi.CreateAnonymousAccountAsync();
            _fixture.Token = response?.Data?.CreateAnonymousAccount?.AccessToken;

            // Assert
            Assert.NotNull(_fixture.Token);
            Assert.True(response?.Errors == null || response.Errors.Count == 0, response?.Errors?[0].Message);

            // Get game settings id
            var response2 = await AidGraphQlApi.GetAccountInfoAsync(_fixture.Token);
            Assert.True(response2?.Errors == null || response2.Errors.Count == 0, response2?.Errors?[0].Message);

            string gameId = response2?.Data.User.GameSettings.Id;
            Assert.NotNull(gameId);

            // Disable safe mode
            var response3 = await AidGraphQlApi.DisableSafeModeAsync(_fixture.Token, gameId, true);
            Assert.True(response3?.Errors == null || response3.Errors.Count == 0, response3?.Errors?[0].Message);
        }

        [Fact]
        [TestPriority(1)]
        public async Task CreateAdventureTest()
        {
            var api = new AidAPI(_fixture.Token);
            var rng = new Random();

            // Get mode list
            var response = await api.SendWebSocketRequestAsync(new WebSocketRequest(AidAPI.AllScenariosId, RequestType.GetScenario));

            AssertResponseIsValid(response);

            var content = response.Payload.Data.Scenario;

            Assert.NotNull(content?.Options);
            Assert.NotEmpty(content.Options);

            var filteredModeList = content.Options
                .Where(x => !x.Title.Contains("custom", StringComparison.OrdinalIgnoreCase))
                .ToList();

            string id = filteredModeList[rng.Next(filteredModeList.Count)].PublicId?.ToString();

            // Get scenario list from a random mode
            response = await api.SendWebSocketRequestAsync(new WebSocketRequest(id, RequestType.GetScenario));

            AssertResponseIsValid(response);

            content = response.Payload.Data.Scenario;

            Assert.NotNull(content?.Options);
            Assert.NotEmpty(content.Options);

            var filteredScenarioList = content.Options
                .Where(x => !x.Title.Contains("custom", StringComparison.OrdinalIgnoreCase))
                .ToList();

            id = filteredScenarioList[rng.Next(filteredScenarioList.Count)].PublicId?.ToString();

            // Get character from a random scenario
            response = await api.SendWebSocketRequestAsync(new WebSocketRequest(id, RequestType.GetScenario));

            AssertResponseIsValid(response);

            content = response.Payload.Data.Scenario;

            Assert.NotNull(content);

            // Create adventure
            response = await api.SendWebSocketRequestAsync(new WebSocketRequest(content.Id, RequestType.CreateAdventure,
                content.Prompt.Replace("${character.name}", "Fergun", StringComparison.OrdinalIgnoreCase)));

            AssertResponseIsValid(response);

            string publicId = response.Payload.Data.AddAdventure?.PublicId?.ToString();

            Assert.NotNull(publicId);

            // Get adventure
            response = await api.SendWebSocketRequestAsync(new WebSocketRequest(publicId, RequestType.GetAdventure));

            AssertResponseIsValid(response);

            var adventure = response.Payload.Data.Adventure;
            _fixture.NormalAdventureId = adventure.PublicId?.ToString();

            Assert.NotNull(adventure);

            var actionList = adventure.Actions;

            Assert.NotNull(actionList);

            actionList.RemoveAll(x => string.IsNullOrEmpty(x.Text));

            Assert.NotEmpty(actionList);

            // Get initial prompt
            string initialPrompt = actionList[^1].Text;
            if (actionList.Count > 1)
            {
                actionList.RemoveAt(actionList.Count - 1);
                initialPrompt = string.Concat(actionList.Select(x => x.Text)) + initialPrompt;
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

            var api = new AidAPI(_fixture.Token);
            var rng = new Random();
            string text = "";

            // Generate random text based on the initial prompt
            for (int i = 0; i < 20; i++)
            {
                text += $" {_fixture.InitialPromptWords[rng.Next(_fixture.InitialPromptWords.Length)]}";
            }

            // Act
            var response = await api.SendWebSocketRequestAsync(_fixture.NormalAdventureId, actionType, text);

            // Assert
            AssertResponseIsValid(response);

            var actionList = response.Payload.Data.SubscribeAdventure?.Actions ?? response.Payload.Data.Adventure?.Actions;
            Assert.NotNull(actionList);
            Assert.NotEmpty(actionList);
        }

        [Fact]
        [TestPriority(3)]
        public async Task GetAdventureTest()
        {
            // Arrange
            var api = new AidAPI(_fixture.Token);

            // Act
            var response = await api.SendWebSocketRequestAsync(new WebSocketRequest(_fixture.NormalAdventureId, RequestType.GetAdventure));

            // Assert
            AssertResponseIsValid(response);

            var actionList = response.Payload.Data.Adventure.Actions;
            Assert.NotNull(actionList);
            Assert.NotEmpty(actionList);

            var lastAction = actionList[^1];

            _fixture.LastActionId = long.Parse(lastAction.Id, CultureInfo.InvariantCulture);
        }

        [Fact]
        [TestPriority(4)]
        public async Task AlterAdventureTest()
        {
            // Arrange
            var api = new AidAPI(_fixture.Token);
            var rng = new Random();
            string text = "";

            // Generate random text based on the initial prompt
            for (int i = 0; i < 20; i++)
            {
                text += $" {_fixture.InitialPromptWords[rng.Next(_fixture.InitialPromptWords.Length)]}";
            }

            // Act
            var response = await api.SendWebSocketRequestAsync(_fixture.NormalAdventureId, ActionType.Alter, text, _fixture.LastActionId);

            // Assert
            AssertResponseIsValid(response);

            var actionList = response.Payload.Data.SubscribeAdventure?.Actions ?? response.Payload.Data.Adventure?.Actions;
            Assert.NotNull(actionList);
            Assert.NotEmpty(actionList);
        }

        [Fact]
        [TestPriority(5)]
        public async Task DeleteAdventureTest()
        {
            // Arrange
            var api = new AidAPI(_fixture.Token);

            // Act
            var response = await api.SendWebSocketRequestAsync(new WebSocketRequest(_fixture.NormalAdventureId, RequestType.DeleteAdventure));

            // Assert
            AssertResponseIsValid(response);
        }

        private static void AssertResponseIsValid(WebSocketResponse response)
        {
            Assert.NotNull(response);
            Assert.True(response.Payload?.Message == null, response.Payload?.Message);
            Assert.True(response.Payload?.Errors == null || response.Payload?.Errors?.Count == 0, response.Payload?.Errors?[0].Message);

            var data = response.Payload?.Data;
            Assert.NotNull(data);

            Assert.True(data.Errors == null || data.Errors?.Count == 0, data.Errors?[0].Message);
            Assert.True(data.AddAction?.Message == null, data.AddAction?.Message);
            Assert.True(data.EditAction?.Message == null, data.EditAction?.Message);
            Assert.True(data.SubscribeAdventure?.Error.Message == null, data.SubscribeAdventure?.Error.Message);
            Assert.True(data.Adventure?.Error?.Message == null, data.Adventure?.Error?.Message);
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