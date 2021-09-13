using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Fergun.Tests
{
    public class CaptionBotTests
    {
        [Theory(Skip = "Disabled until Microsoft fixes the CaptionBot API")]
        [InlineData("https://cdn.discordapp.com/attachments/680588007333560404/815078027599413268/unknown.png")]
        [InlineData("https://cdn.discordapp.com/attachments/680588007333560404/815077929037856818/unknown.png")]
        [InlineData("https://cdn.discordapp.com/attachments/680588007333560404/815078216975777802/unknown.png")]
        [InlineData("https://cdn.discordapp.com/attachments/680588007333560404/815078475211210812/unknown.png")]
        public async Task CaptionBotAvailableTest(string url)
        {
            // Arrange
            var data = new Dictionary<string, string>
            {
                { "Content", url },
                { "Type", "CaptionRequest" }
            };

            using var content = new FormUrlEncodedContent(data);
            using var httpClient = new HttpClient();

            // Act
            var response = await httpClient.PostAsync(new Uri("https://captionbot2.azurewebsites.net/api/messages?language=en-US"), content);
            string text = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.NotNull(text);
        }
    }
}