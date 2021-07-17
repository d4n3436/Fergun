using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Discord;
using Fergun.Interactive.Pagination;

namespace Fergun.Utils
{
    public static class CommandUtils
    {
        private static Dictionary<IEmote, PaginatorAction> _fergunPaginatorEmotes;

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

        public static async Task<string> ParseGeniusLyricsAsync(string url, bool keepHeaders = false)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            var document = await context.OpenAsync(url);

            var element = document
                .All
                .FirstOrDefault(x =>
                x?.ClassName != null
                && x.NodeType == NodeType.Element
                && x.NodeName == "DIV"
                && (x.ClassName.Contains("Lyrics__Root", StringComparison.Ordinal) || x.ClassName.Contains("lyrics", StringComparison.Ordinal)));

            if (element == null)
            {
                return null;
            }

            string innerHtml = element.GetElementsByClassName("lyrics")?.FirstOrDefault()?.InnerHtml
                ?? string.Concat(element
                .Children
                .Where(x =>
                x?.ClassName != null
                && x.NodeType == NodeType.Element
                && x.NodeName == "DIV"
                && x.ClassName.Contains("Lyrics__Container", StringComparison.Ordinal))
                .Select(x => x.InnerHtml));

            if (string.IsNullOrEmpty(innerHtml))
            {
                return null;
            }

            // Remove newlines, tabs and empty HTML tags.
            string lyrics = Regex.Replace(innerHtml, @"\t|\n|\r|<[^/>][^>]*>\s*<\/[^>]+>", string.Empty);

            lyrics = WebUtility.HtmlDecode(lyrics)
                .Replace("<b>", "**", StringComparison.Ordinal)
                .Replace("</b>", "**", StringComparison.Ordinal)
                .Replace("<i>", "*", StringComparison.Ordinal)
                .Replace("</i>", "*", StringComparison.Ordinal)
                .Replace("<br>", "\n", StringComparison.Ordinal)
                .Replace("</div>", "\n", StringComparison.Ordinal);

            // Remove remaining HTML tags.
            lyrics = Regex.Replace(lyrics, @"(\<.*?\>)", string.Empty);

            // Prevent bold text overlapping.
            lyrics = lyrics.Replace("****", "** **", StringComparison.Ordinal)
                .Replace("******", "*** ****", StringComparison.Ordinal);

            if (!keepHeaders)
            {
                lyrics = Regex.Replace(lyrics, @"(\[.*?\])*", string.Empty);
            }
            return Regex.Replace(lyrics, @"\n{3,}", "\n\n").Trim();
        }

#if DNETLABS
         public static MessageComponent BuildLinks(IMessageChannel channel)
        {
            var builder = new ComponentBuilder();
            if (FergunClient.InviteLink != null && Uri.IsWellFormedUriString(FergunClient.InviteLink, UriKind.Absolute))
            {
                builder.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("Invite", channel), FergunClient.InviteLink));
            }

            if (FergunClient.DblBotPage != null && Uri.IsWellFormedUriString(FergunClient.DblBotPage, UriKind.Absolute))
            {
                builder.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("DBLBotPage", channel), FergunClient.DblBotPage));
                //.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("VoteLink", channel), $"{FergunClient.DblBotPage}/vote"));
            }

            if (FergunClient.Config.SupportServer != null && Uri.IsWellFormedUriString(FergunClient.Config.SupportServer, UriKind.Absolute))
            {
                builder.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("SupportServer", channel), FergunClient.Config.SupportServer));
            }

            if (FergunClient.Config.DonationUrl != null && Uri.IsWellFormedUriString(FergunClient.Config.DonationUrl, UriKind.Absolute))
            {
                builder.WithButton(ButtonBuilder.CreateLinkButton(GuildUtils.Locate("Donate", channel), FergunClient.Config.DonationUrl));
            }

            return builder.Build();
        }
#else
        public static string BuildLinks(IMessageChannel channel)
        {
            string links = $"{Format.Url(GuildUtils.Locate("Invite", channel), FergunClient.InviteLink)}";
            if (FergunClient.DblBotPage != null)
            {
                links += $" | {Format.Url(GuildUtils.Locate("DBLBotPage", channel), FergunClient.DblBotPage)}";
                links += $" | {Format.Url(GuildUtils.Locate("VoteLink", channel), $"{FergunClient.DblBotPage}/vote")}";
            }
            if (!string.IsNullOrEmpty(FergunClient.Config.SupportServer))
            {
                links += $" | {Format.Url(GuildUtils.Locate("SupportServer", channel), FergunClient.Config.SupportServer)}";
            }
            if (!string.IsNullOrEmpty(FergunClient.Config.DonationUrl))
            {
                links += $" | {Format.Url(GuildUtils.Locate("Donate", channel), FergunClient.Config.DonationUrl)}";
            }

            return links;
        }
#endif

        public static Dictionary<IEmote, PaginatorAction> GetFergunPaginatorEmotes(FergunConfig config)
        {
            if (_fergunPaginatorEmotes != null)
            {
                return _fergunPaginatorEmotes;
            }

            _fergunPaginatorEmotes = new Dictionary<IEmote, PaginatorAction>();

            AddEmote(config.FirstPageEmote, PaginatorAction.SkipToStart, "⏮");
            AddEmote(config.PreviousPageEmote, PaginatorAction.Backward, "◀");
            AddEmote(config.NextPageEmote, PaginatorAction.Forward, "▶");
            AddEmote(config.LastPageEmote, PaginatorAction.SkipToEnd, "⏭");
            AddEmote(config.StopPaginatorEmote, PaginatorAction.Exit, "🛑");

            return _fergunPaginatorEmotes;

            static void AddEmote(string emoteString, PaginatorAction action, string defaultEmoji)
            {
                var emote = string.IsNullOrEmpty(emoteString) || !Emote.TryParse(emoteString, out var parsedEmote)
                    ? new Emoji(defaultEmoji) as IEmote
                    : parsedEmote;

                _fergunPaginatorEmotes.Add(emote, action);
            }
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