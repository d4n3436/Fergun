using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using Discord;

namespace Fergun.Utils
{
    public static class CommandUtils
    {
        public static async Task<double> GetCpuUsageForProcessAsync()
        {
            var startTime = DateTimeOffset.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            await Task.Delay(500);

            var endTime = DateTimeOffset.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }

        public static async Task<string> ParseGeniusLyricsAsync(string url, bool keepHeaders)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            var document = await context.OpenAsync(url);
            var element = document?.GetElementsByClassName("lyrics")?.FirstOrDefault()
                       ?? document?.GetElementsByClassName("SongPageGrid-sc-1vi6xda-0 DGVcp Lyrics__Root-sc-1ynbvzw-0 kkHBOZ")?.FirstOrDefault();

            if (element == null)
            {
                return null;
            }

            // Remove newlines, tabs and empty HTML tags.
            string lyrics = Regex.Replace(element.InnerHtml, @"\t|\n|\r|<[^/>][^>]*>\s*<\/[^>]+>", string.Empty);

            lyrics = WebUtility.HtmlDecode(lyrics)
                .Replace("<b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("</b>", "**", StringComparison.OrdinalIgnoreCase)
                .Replace("<i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("</i>", "*", StringComparison.OrdinalIgnoreCase)
                .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

            // Remove remaining HTML tags.
            lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);

            // Prevent bold text overlapping.
            lyrics = lyrics.Replace("****", "** **", StringComparison.OrdinalIgnoreCase)
                .Replace("******", "*** ****", StringComparison.OrdinalIgnoreCase);

            if (!keepHeaders)
            {
                lyrics = Regex.Replace(lyrics, @"(\[.*?\])*", string.Empty);
            }
            return Regex.Replace(lyrics, @"\n{3,}", "\n\n").Trim();
        }

        public static MessageComponent BuildLinks(IMessageChannel channel)
        {
            // TODO: Undo these changes after the nullref fix
            var components = new List<IMessageComponent>();

            if (FergunClient.InviteLink != null && Uri.IsWellFormedUriString(FergunClient.InviteLink, UriKind.Absolute))
            {
                components.Add(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("Invite", channel), FergunClient.InviteLink).Build());
            }

            if (FergunClient.DblBotPage != null && Uri.IsWellFormedUriString(FergunClient.DblBotPage, UriKind.Absolute))
            {
                components.Add(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("DBLBotPage", channel), FergunClient.DblBotPage).Build());
                //.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("VoteLink", channel), $"{FergunClient.DblBotPage}/vote"));
            }

            if (FergunClient.Config.SupportServer != null && Uri.IsWellFormedUriString(FergunClient.Config.SupportServer, UriKind.Absolute))
            {
                components.Add(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("SupportServer", channel), FergunClient.Config.SupportServer).Build());
            }

            if (FergunClient.Config.DonationUrl != null && Uri.IsWellFormedUriString(FergunClient.Config.DonationUrl, UriKind.Absolute))
            {
                components.Add(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("Donate", channel), FergunClient.Config.DonationUrl).Build());
            }

            return new ComponentBuilder { ActionRows = new List<ActionRowBuilder> { new ActionRowBuilder().WithComponents(components) } }.Build();
        }

        public static IEmote ParseEmoteOrEmoji(string emote)
        {
            if (string.IsNullOrEmpty(emote))
                return null;

            return Emote.TryParse(emote, out var temp)
                ? (IEmote)temp
                : new Emoji(FergunClient.Config.FirstPageEmote);
        }

        public static string RunCommand(string command)
        {
            // TODO: Add support to the remaining platforms
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!isLinux && !isWindows)
                return null;

            var escapedArgs = command.Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase);
            var startInfo = new ProcessStartInfo
            {
                FileName = isLinux ? "/bin/bash" : "cmd.exe",
                Arguments = isLinux ? $"-c \"{escapedArgs}\"" : $"/c {escapedArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = isLinux ? "/home" : ""
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit(10000);

            return process.ExitCode == 0
                ? process.StandardOutput.ReadToEnd()
                : process.StandardError.ReadToEnd();
        }
    }
}