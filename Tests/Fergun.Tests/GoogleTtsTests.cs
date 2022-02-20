using System;
using System.IO;
using System.Threading.Tasks;
using Fergun.APIs;
using GTranslate.Translators;
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
            var translator = new GoogleTranslator2();
            var stream = await translator.TextToSpeechAsync(text, language, slow);

            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            // Assert
            Assert.NotEmpty(ms.ToArray());
        }

        [Theory]
        [InlineData(null, "es")]
        [InlineData("Hello world", null)]
        public async Task TtsInvalidArgumentTest(string text, string language)
        {
            var translator = new GoogleTranslator2();

            // Act and Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await translator.TextToSpeechAsync(text, language));
        }
    }
}