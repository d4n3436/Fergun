using System;
using System.Threading.Tasks;
using Fergun.APIs;
using Xunit;

namespace Fergun.Tests
{
    public class GoogleTtsTests
    {
        [Theory]
        [InlineData("Hello world", "en", false)]
        [InlineData("Hola mundo", "es", false)]
        [InlineData("The quick brown fox jumps over the lazy dog.", "en", true)]
        [InlineData("La rapida volpe marrone salta sopra il cane pigro.", "it", true)]
        [InlineData("El zorro marrón rápido salta sobre el perro perezoso.", "es", false)]
        [InlineData("Discord is an American VoIP, instant messaging and digital distribution platform designed for creating communities.", "en", true)]
        public async Task TtsAvailableTest(string text, string language, bool slow)
        {
            // Act
            var results = await GoogleTTS.GetTtsAsync(text, language, slow);

            // Assert
            Assert.NotEmpty(results);
        }

        [Theory]
        [InlineData("", "es")]
        [InlineData("Hello world", "")]
        public async Task TtsInvalidArgumentTest(string text, string language)
        {
            // Act and Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await GoogleTTS.GetTtsAsync(text, language));
        }
    }
}