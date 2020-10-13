using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;

namespace Fergun.Utils
{
    public static class CommandUtils
    {
        public static async Task<double> GetCpuUsageForProcessAsync()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            await Task.Delay(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }

        public static async Task<string> ParseGeniusLyricsAsync(Uri uri, bool keepHeaders)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            var document = await context.OpenAsync(uri.AbsoluteUri);
            var element = document?.GetElementsByClassName("lyrics")?.FirstOrDefault()
                       ?? document?.GetElementsByClassName("SongPageGrid-sc-1vi6xda-0 DGVcp Lyrics__Root-sc-1ynbvzw-0 kkHBOZ")?.FirstOrDefault();

            if (element == null)
            {
                return null;
            }

            // Remove newlines and tabs.
            string lyrics = Regex.Replace(element.InnerHtml, @"\t|\n|\r", string.Empty);

            lyrics = WebUtility.HtmlDecode(lyrics)
                .Replace("<b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("</b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("<i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("</i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

            // Remove remaining HTML tags.
            lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);

            if (!keepHeaders)
            {
                lyrics = Regex.Replace(lyrics, @"(\[.*?\])*", string.Empty, RegexOptions.Multiline);
            }
            return Regex.Replace(lyrics, @"\n{3,}", "\n\n").Trim();
        }
    }
}