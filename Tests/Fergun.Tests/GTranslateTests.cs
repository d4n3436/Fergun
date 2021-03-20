using System.Threading.Tasks;
using Fergun.APIs.GTranslate;
using Xunit;

namespace Fergun.Tests
{
    public class GTranslateTests
    {
        [Theory]
        [InlineData("Hello World", "es")]
        [InlineData("Hola Mundo", "en")]
        [InlineData("The quick brown fox jumps over the lazy dog.", "it")]
        [InlineData("El zorro marrón rápido salta sobre el perro perezoso.", "fr")]
        [InlineData("Discord is an American VoIP, instant messaging and digital distribution platform designed for creating communities.", "de")]
        public async Task TranslationNotEmptyTest(string text, string to)
        {
            // Arrange
            using var translator = new GTranslator();

            // Act
            var results = await translator.TranslateAsync(text, to);

            // Assert
            Assert.NotNull(results?.Translation);
        }
    }
}