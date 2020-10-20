using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Fergun.Utils
{
    public static class StringUtils
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<HttpResponseMessage> GetUrlResponseHeadersAsync(string url)
        {
            try
            {
                return await _httpClient.GetAsync(new UriBuilder(url).Uri, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception e) when (e is HttpRequestException || e is UriFormatException || e is ArgumentException)
            {
                return null;
            }
        }

        public static async Task<long?> GetUrlContentLengthAsync(string url)
        {
            var response = await GetUrlResponseHeadersAsync(url);
            return response?.Content?.Headers?.ContentLength;
        }

        public static async Task<string> GetUrlMediaTypeAsync(string url)
        {
            var response = await GetUrlResponseHeadersAsync(url);
            return response?.Content?.Headers?.ContentType?.MediaType;
        }

        public static async Task<bool> IsImageUrlAsync(string url)
        {
            string mediaType = await GetUrlMediaTypeAsync(url);
            return mediaType != null && mediaType.ToLowerInvariant().StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        public static string RandomString(int length, Random rng)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[rng.Next(s.Length)]).ToArray());
        }

        public static string ReadPassword()
        {
            string password = string.Empty;
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return password;
                }
                password += keyInfo.KeyChar;
            }
        }
    }
}