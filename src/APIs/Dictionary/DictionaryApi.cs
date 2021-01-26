using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.Dictionary
{
    /// <summary>
    /// Represents a dictionary API.
    /// </summary>
    public static class DictionaryApi
    {
        /// <summary>
        /// Returns the API endpoint.
        /// </summary>
        public const string ApiEndpoint = "https://api.dictionaryapi.dev/api/v2/entries/";

        /// <summary>
        /// Returns the default language.
        /// </summary>
        public const string DefaultLanguage = "en";

        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri(ApiEndpoint) };

        /// <summary>
        /// Gets word definitions using the provided word and language.
        /// </summary>
        /// <param name="word">The word to get its definitions.</param>
        /// <param name="language">A language in <see cref="SupportedLanguages"/>.</param>
        /// <param name="fallback">Whether to fallback to <see cref="DefaultLanguage"/> if there are no results in <paramref name="language"/>.</param>
        /// <returns>A task representing the asynchronous operation. The result contains a read-only list of <see cref="DefinitionCategory"/> objects.</returns>
        public static async Task<IReadOnlyList<DefinitionCategory>> GetDefinitionsAsync(string word, string language = DefaultLanguage, bool fallback = false)
        {
            if (string.IsNullOrEmpty(word))
            {
                throw new ArgumentNullException(nameof(word));
            }

            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentNullException(nameof(language));
            }

            language = language.ToLowerInvariant();
            if (!SupportedLanguages.Contains(language))
            {
                language = DefaultLanguage;
            }

            HttpResponseMessage response;
            while (true)
            {
                response = await _httpClient.GetAsync(new Uri($"{language}/{Uri.EscapeDataString(word)}", UriKind.Relative));

                // No definitions found.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    if (!fallback || language == DefaultLanguage)
                    {
                        return Array.Empty<DefinitionCategory>();
                    }

                    // Fallback to the default language.
                    language = DefaultLanguage;
                    fallback = false;
                    continue;
                }

                break;
            }

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IReadOnlyList<DefinitionCategory>>(json);
        }

        /// <summary>
        /// Gets a read-only list containing the supported languages.
        /// </summary>
        public static IReadOnlyList<string> SupportedLanguages { get; } = new[]
        {
            "en",
            "hi",
            "es",
            "fr",
            "ja",
            "ru",
            "de",
            "it",
            "ko",
            "pt-BR",
            "ar",
            "tr"
        };
    }
}