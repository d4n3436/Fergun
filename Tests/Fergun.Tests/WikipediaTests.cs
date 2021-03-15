using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Fergun.Responses;
using Newtonsoft.Json;
using Xunit;

namespace Fergun.Tests
{
    public class WikipediaTests
    {
        [Theory]
        [InlineData("wikipedia")]
        [InlineData("discord")]
        [InlineData("windows 10")]
        [InlineData("c#")]
        public async Task WikipediaAvailableTest(string query)
        {
            // Arrange
            using var httpClient = new HttpClient();

            // Act
            string response = await httpClient.GetStringAsync($"https://en.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&format=json");
            var search = JsonConvert.DeserializeObject<List<dynamic>>(response);

            // Assert
            Assert.True(search.Count > 1);
            Assert.NotEmpty(search[1]);

            // Arrange
            string articleUrl = search[^1][0];

            // Act
            response = await httpClient.GetStringAsync($"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(Uri.UnescapeDataString(articleUrl.Substring(30)))}");
            var article = JsonConvert.DeserializeObject<WikiArticle>(response);

            // Assert
            Assert.NotNull(article);
        }
    }
}