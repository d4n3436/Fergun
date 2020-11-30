using System;
using System.Net;
using Newtonsoft.Json;

namespace Fergun.APIs.UrbanDictionary
{
    public static class UrbanApi
    {
        public const string ApiEndpoint = "https://api.urbandictionary.com/v0/";

        public static UrbanResponse SearchWord(string word)
        {
            string response;
            using (var wc = new WebClient())
            {
                response = wc.DownloadString($"{ApiEndpoint}define?term={Uri.EscapeDataString(word)}");
            }
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }

        public static UrbanResponse GetRandomWords()
        {
            string response;
            using (var wc = new WebClient())
            {
                response = wc.DownloadString($"{ApiEndpoint}random");
            }
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }
    }
}