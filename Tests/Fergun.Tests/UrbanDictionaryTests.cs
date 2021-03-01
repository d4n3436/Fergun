using System.Net;
using Fergun.APIs.UrbanDictionary;
using Xunit;

namespace Fergun.Tests
{
    public class UrbanDictionaryTests
    {
        [Theory]
        [InlineData("lol")]
        [InlineData("afaik")]
        [InlineData("cringe")]
        [InlineData("pog")]
        [InlineData("gg")]
        public void SearchNotEmptyTest(string word)
        {
            // Act
            var response = UrbanApi.SearchWord(word);

            // Assert
            Assert.NotEmpty(response.Definitions);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("!")]
        public void InvalidSearchTest(string word)
        {
            // Act and Assert
            Assert.Throws<WebException>(() => UrbanApi.SearchWord(word));
        }

        [Fact]
        public void RandomSearchNotEmptyTest()
        {
            // Act
            var response = UrbanApi.GetRandomWords();

            // Assert
            Assert.NotEmpty(response.Definitions);
        }
    }
}