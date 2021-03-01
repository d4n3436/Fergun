using System.Threading.Tasks;
using Fergun.APIs.DiscordBots;
using Xunit;

namespace Fergun.Tests
{
    public class DiscordBotsTests
    {
        [Theory]
        [InlineData("bot")]
        [InlineData("discord")]
        [InlineData("music")]
        public async Task DiscordBotsAvailableTest(string query)
        {
            // Act
            var result = await DiscordBotsApi.GetBotsAsync(query);

            // Assert
            Assert.NotEmpty(result.Bots);

            // Act
            var bot = await DiscordBotsApi.GetBotAsync(ulong.Parse(result.Bots[0].UserId));

            // Assert
            Assert.NotNull(bot);
        }
    }
}