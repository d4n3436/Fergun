using System.Net;
using System.Web;
using Newtonsoft.Json;

namespace Fergun.APIs.UrbanDictionary
{
    public class UrbanApi
    {
        public string ApiEndpoint { get; } = "https://api.urbandictionary.com/v0/";

        public UrbanApi()
        {

        }

        public UrbanApi(string NewApiEndpoint)
        {
            ApiEndpoint = NewApiEndpoint;
        }

        public UrbanResponse SearchWord(string word)
        {
            string response;
            using (WebClient wc = new WebClient())
            {
                response = wc.DownloadString($"{ApiEndpoint}define?term={HttpUtility.UrlPathEncode(word)}");
            }
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }

        public UrbanResponse GetRandomWords()
        {
            string response;
            using (WebClient wc = new WebClient())
            {
                response = wc.DownloadString($"{ApiEndpoint}random");
            }
            return JsonConvert.DeserializeObject<UrbanResponse>(response);
        }
    }
}