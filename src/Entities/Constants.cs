﻿using Fergun.Interactive;

namespace Fergun;

public static class Constants
{
    public const double GlobalRatelimitPeriod = 10; // Seconds

    public const int GlobalCommandUsesPerPeriod = 3;

    public const string GitHubRepositoryUrl = "https://github.com/d4n3436/Fergun";

    public const string GoogleLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890326437268168704/unknown.png";

    public const string GoogleLensLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/1113265509899702293/google_lens.png";

    public const string GoogleTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838833843917029446/googletranslate.png";

    public const string BingTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944755269034991666/BingTranslator.png";

    public const string MicrosoftAzureLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944745954605686864/Microsoft_Azure.png";

    public const string YandexTranslateLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/857013120358416394/YandexTranslate.png";

    public const string DuckDuckGoLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323046286651402/unknown.png";

    public const string BraveLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/890323194504937522/unknown.png";

    public const string BadTranslatorLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/944755022816763914/unknown.png";

    public const string BingIconUrl = "https://cdn.discordapp.com/attachments/838832564583661638/949767220232339507/Bing_Icon.png";

    public const string YandexIconUrl = "https://cdn.discordapp.com/attachments/838832564583661638/954428306533523476/Yandex_Icon.png";

    public const string UrbanDictionaryIconUrl = "https://cdn.discordapp.com/attachments/838832564583661638/951936600273715300/UrbanDictionary.png";

    public const string WolframAlphaLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/838834461638131722/wolframalpha.png";

    public const string GeniusLogoUrl = "https://cdn.discordapp.com/attachments/838832564583661638/1114311596450254980/genius_logo.png";

    public const string GoogleUrl = "https://google.com";

    public const string DuckDuckGoUrl = "https://duckduckgo.com";

    public const string YandexImageSearchUrl = "https://yandex.com/images";

    public const string BingVisualSearchUrl = "https://www.bing.com/visualsearch";

    public const string GoogleLensUrl = "https://lens.google.com";

    public const string ChromeUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";

    public const ActionOnStop DefaultPaginatorActionOnCancel = ActionOnStop.DeleteMessage;

    public const ActionOnStop DefaultPaginatorActionOnTimeout = ActionOnStop.DisableInput;
}