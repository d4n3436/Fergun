using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Fergun.APIs;
using Fergun.APIs.BingTranslator;
using Fergun.APIs.Dictionary;
using Fergun.APIs.DuckDuckGo;
using Fergun.APIs.OCRSpace;
using Fergun.APIs.UrbanDictionary;
using Fergun.APIs.WaybackMachine;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Responses;
using Fergun.Services;
using Fergun.Utils;
using GoogleTranslateFreeApi;
using NCalc;
using Newtonsoft.Json;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace Fergun.Modules
{
    [RequireBotPermission(Constants.MinimunRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class Utility : FergunBase
    {
        [ThreadStatic]
        private static Random _rng;
        private static readonly Regex _bracketRegex = new Regex(@"\[(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled); // \[(\[*.+?]*)\]
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = Constants.HttpClientTimeout };
        private static readonly YoutubeClient _ytClient = new YoutubeClient();
        private static XkcdComic _lastComic;
        private static DateTimeOffset _timeToCheckComic = DateTimeOffset.UtcNow;
        private static CommandService _cmdService;
        private static LogService _logService;

        private static Random RngInstance => _rng ??= new Random();

        public Utility(CommandService commands, LogService logService)
        {
            _cmdService ??= commands;
            _logService ??= logService;
        }

        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("archive", RunMode = RunMode.Async), Ratelimit(1, 1.5, Measure.Minutes)]
        [Summary("archiveSummary")]
        [Remarks("TimestampFormat")]
        [Alias("waybackmachine", "wb")]
        public async Task<RuntimeResult> Archive([Summary("archiveParam1")] string url, [Summary("archiveParam2")] ulong timestamp)
        {
            double length = Math.Floor(Math.Log10(timestamp) + 1);
            if (length < 4 || length > 14)
            {
                return FergunResult.FromError(Locate("InvalidTimestamp"));
            }

            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Invalid url: {Uri.UnescapeDataString(url)}"));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Url: {uri.AbsoluteUri}"));

            WaybackResponse waybackResponse;
            try
            {
                waybackResponse = await WaybackApi.GetSnapshotAsync(uri.AbsoluteUri, timestamp);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in Wayback Machine API, url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in Wayback Machine API, url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            var snapshot = waybackResponse.ArchivedSnapshots?.Closest;

            if (snapshot == null)
            {
                return FergunResult.FromError(Locate("NoSnapshots"));
            }

            bool success = DateTime.TryParseExact(snapshot.Timestamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var datetime);

            var builder = new EmbedBuilder()
                .WithTitle("Wayback Machine")
                .AddField("Url", Format.Url(Locate("ClickHere"), snapshot.Url))
                .AddField(Locate("Timestamp"), success ? datetime.ToString() : "?")
                .WithColor(FergunClient.Config.EmbedColor);

            if (string.IsNullOrEmpty(FergunClient.Config.ApiFlashAccessKey))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Archive: ApiFlash access key is null or empty, sending only the embed."));

                await ReplyAsync(embed: builder.Build());

                return FergunResult.FromSuccess();
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Requesting screenshot of url: {snapshot.Url}"));
            ApiFlashResponse response;
            try
            {
                response = await ApiFlash.UrlToImageAsync(FergunClient.Config.ApiFlashAccessKey, snapshot.Url, ApiFlash.FormatType.png, "400,403,404,500-511");
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Archive: Error in ApiFlash API", e));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            catch (WebException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Archive: Error in ApiFlash API", e));
                return FergunResult.FromError(e.Message);
            }

            if (response.ErrorMessage != null)
            {
                return FergunResult.FromError(response.ErrorMessage);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Archive: Sending the embed..."));
            try
            {
                builder.ImageUrl = "attachment://screenshot.png";
                using (Stream image = await _httpClient.GetStreamAsync(new Uri(response.Url)))
                {
                    await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, image, "screenshot.png", embed: builder.Build());
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            return FergunResult.FromSuccess();
        }

        [Command("avatar", RunMode = RunMode.Async)]
        [Summary("avatarSummary")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> Avatar([Remainder, Summary("avatarParam1")] IUser user = null)
        {
            user ??= Context.User;
            if (!(user is RestUser))
            {
                // Prevent getting error 404 while downloading the avatar getting the user from REST.
                user = await Context.Client.Rest.GetUserAsync(user.Id);
            }

            string avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();
            string thumbnail = user.GetAvatarUrl(ImageFormat.Png, 128) ?? user.GetDefaultAvatarUrl();

            System.Drawing.Color avatarColor;
            try
            {
                using (Stream response = await _httpClient.GetStreamAsync(new Uri(thumbnail)))
                using (Bitmap img = new Bitmap(response))
                {
                    avatarColor = img.GetAverageColor();
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            var builder = new EmbedBuilder
            {
                Title = user.ToString(),
                ImageUrl = avatarUrl,
                Color = new Discord.Color(avatarColor.R, avatarColor.G, avatarColor.B)
            };

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("badtranslator", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("badtranslatorSummary")]
        [Alias("bt")]
        [Example("i don't know what to say lol")]
        public async Task<RuntimeResult> Badtranslator([Remainder, Summary("badtranslatorParam1")] string text)
        {
            List<string> languageChain = new List<string>();
            int chainCount = 7;
            string originalLang = null;
            SimpleTranslationResult translation;

            for (int i = 0; i < chainCount; i++)
            {
                string targetLang;
                if (i == chainCount - 1)
                {
                    targetLang = originalLang;
                }
                else
                {
                    // Get unique and random languages.
                    do
                    {
                        targetLang = BingTranslatorApi.SupportedLanguages[RngInstance.Next(BingTranslatorApi.SupportedLanguages.Count)];
                    } while (languageChain.Contains(targetLang));
                }

                translation = await TranslateSimpleAsync(text, targetLang, "");
                if (translation.Error != null)
                {
                    return FergunResult.FromError(Locate(translation.Error));
                }
                if (i == 0)
                {
                    originalLang = translation.Source.ISO639;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Badtranslator: Original language: {originalLang}"));

                    // Fallback to English if the detected language is not supported by Bing.
                    if (!BingTranslatorApi.SupportedLanguages.Any(x => x == originalLang))
                    {
                        await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Badtranslator: Original language not supported by Bing. Fallback to English."));
                        originalLang = "en";
                    }
                    languageChain.Add(originalLang);
                }
                text = translation.Text;
                languageChain.Add(targetLang);
            }

            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                try
                {
                    var response = await Hastebin.UploadAsync(text);
                    text = Format.Url(Locate("HastebinLink"), response.GetLink());
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Paste: Error while uploading text to Hastebin", e));
                    text = text.Truncate(EmbedFieldBuilder.MaxFieldValueLength);
                }
            }

            var builder = new EmbedBuilder()
                .WithTitle("Bad translator")
                .AddField(Locate("LanguageChain"), string.Join(" -> ", languageChain))
                .AddField(Locate("Result"), text)
                .WithThumbnailUrl("https://fergun.is-inside.me/gXEDLZVr.png")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("base64decode")]
        [Summary("base64decodeSummary")]
        [Alias("b64decode", "b64d")]
        [Example("aGVsbG8gd29ybGQ=")]
        public async Task<RuntimeResult> Base64decode([Remainder, Summary("base64decodeParam1")] string text)
        {
            if (!text.IsBase64())
            {
                return FergunResult.FromError(Locate("base64decodeInvalid"));
            }

            await ReplyAsync(Encoding.UTF8.GetString(Convert.FromBase64String(text)), allowedMentions: AllowedMentions.None);
            return FergunResult.FromSuccess();
        }

        [Command("base64encode")]
        [Summary("base64encodeSummary")]
        [Alias("b64encode", "b64e")]
        [Example("hello")]
        public async Task Base64encode([Remainder, Summary("base64encodeParam1")] string text)
        {
            text = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            if (text.Length > DiscordConfig.MaxMessageSize)
            {
                try
                {
                    var response = await Hastebin.UploadAsync(text);
                    text = response.GetLink();
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Badtranslator: Error while uploading text to Hastebin", e));
                    text = text.Truncate(DiscordConfig.MaxMessageSize);
                }
            }
            await ReplyAsync(text);
        }

        [Command("bigeditsnipe", RunMode = RunMode.Async)]
        [Summary("bigeditsnipeSummary")]
        [Alias("besnipe", "bes")]
        public async Task<RuntimeResult> Bigeditsnipe([Summary("bigeditsnipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var msgs = FergunClient.MessageCache
                .Where(x => x.SourceEvent == SourceEvent.MessageUpdated && x.Channel.Id == channel.Id)
                .OrderByDescending(x => x.CachedAt)
                .Take(20);

            var builder = new EmbedBuilder();
            if (!msgs.Any())
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                var text = msgs.Select(x =>
                $"{Format.Bold(x.Author.ToString())} ({string.Format(Locate("MinutesAgo"), (DateTimeOffset.UtcNow - x.CreatedAt).Minutes)})\n{x.Content.Truncate(200)}\n\n");

                builder.WithTitle("Big edit snipe")
                    .WithDescription(string.Concat(text).Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{channel.Name}");
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("bigsnipe", RunMode = RunMode.Async)]
        [Summary("bigsnipeSummary")]
        [Alias("bsnipe", "bs")]
        public async Task<RuntimeResult> Bigsnipe([Summary("bigsnipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var msgs = FergunClient.MessageCache
                .Where(x => x.SourceEvent == SourceEvent.MessageDeleted && x.Channel.Id == channel.Id)
                .OrderByDescending(x => x.CachedAt)
                .Take(20);

            var builder = new EmbedBuilder();
            if (!msgs.Any())
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                string text = "";
                foreach (var msg in msgs)
                {
                    text += $"{Format.Bold(msg.Author.ToString())} ({string.Format(Locate("MinutesAgo"), (DateTimeOffset.UtcNow - msg.CreatedAt).Minutes)})\n";
                    text += !string.IsNullOrEmpty(msg.Content) ? msg.Content.Truncate(200) : msg.Attachments.Count > 0 ? $"({Locate("Attachment")})" : "?";
                    text += "\n\n";
                }
                builder.WithTitle("Big snipe")
                    .WithDescription(text.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{channel.Name}");
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("calc", RunMode = RunMode.Async)]
        [Summary("calcSummary")]
        [Alias("calculate")]
        [Example("2 * 2 - 1")]
        public async Task<RuntimeResult> Calc([Remainder, Summary("calcParam1")] string expression)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Calc: expression: {expression}"));

            Stopwatch sw = Stopwatch.StartNew();
            var ex = new Expression(expression);
            if (ex.HasErrors())
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }
            string result;
            try
            {
                result = ex.Evaluate().ToString();
            }
            catch (ArgumentException)
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }
            sw.Stop();

            if (result.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                result = result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 3) + "...";
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("CalcResults"))
                .AddField(Locate("Input"), Format.Code(expression.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("Output"), Format.Code(result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .WithFooter(string.Format(Locate("EvalFooter"), sw.ElapsedMilliseconds))
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("channelinfo")]
        [Summary("channelinfoSummary")]
        [Alias("channel")]
        [Example("#general")]
        public async Task<RuntimeResult> Channelinfo([Remainder, Summary("channelinfoParam1")] IChannel channel = null)
        {
            channel ??= Context.Channel;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ChannelInfo"))
                .AddField(Locate("Name"), channel.Name, true)
                .AddField("ID", channel.Id, true);

            if (channel is ITextChannel textChannel)
            {
                builder.AddField(Locate("Type"), Locate(channel is SocketNewsChannel ? "AnnouncementChannel" : "TextChannel"), true)
                    .AddField(Locate("Topic"), string.IsNullOrEmpty(textChannel.Topic) ? Locate("None") : textChannel.Topic, true)
                    .AddField(Locate("IsNSFW"), Locate(textChannel.IsNsfw), true)
                    .AddField(Locate("SlowMode"), TimeSpan.FromSeconds(channel is SocketNewsChannel ? 0 : textChannel.SlowModeInterval).ToShortForm2(), true)
                    .AddField(Locate("Category"), textChannel.CategoryId.HasValue ? Context.Guild.GetCategoryChannel(textChannel.CategoryId.Value).Name : Locate("None"), true);
            }
            else if (channel is IVoiceChannel voiceChannel)
            {
                builder.AddField(Locate("Type"), Locate("VoiceChannel"), true)
                    .AddField(Locate("Bitrate"), $"{voiceChannel.Bitrate / 1000}kbps", true)
                    .AddField(Locate("UserLimit"), voiceChannel.UserLimit?.ToString() ?? Locate("NoLimit"), true);
            }
            else if (channel is SocketCategoryChannel categoryChannel)
            {
                builder.AddField(Locate("Type"), Locate("Category"), true)
                    .AddField(Locate("Channels"), categoryChannel.Channels.Count, true);
            }
            else if (channel is IDMChannel)
            {
                builder.AddField(Locate("Type"), Locate("DMChannel"), true);
            }
            if (channel is IGuildChannel guildChannel)
            {
                builder.AddField(Locate("Position"), guildChannel.Position, true);
            }
            builder.AddField(Locate("CreatedAt"), channel.CreatedAt, true)
                .AddField(Locate("Mention"), MentionUtils.MentionChannel(channel.Id), true)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("choice")]
        [Summary("choiceSummary")]
        [Alias("choose")]
        [Example("c c++ c#")]
        public async Task<RuntimeResult> Choice([Summary("choiceParam1")] params string[] choices)
        {
            if (choices.Length == 0)
            {
                return FergunResult.FromError(Locate("NoChoices"));
            }
            await ReplyAsync($"{Locate("IChoose")} **{choices[RngInstance.Next(0, choices.Length)]}**{(choices.Length == 1 ? Locate("OneChoice") : "")}", allowedMentions: AllowedMentions.None);
            return FergunResult.FromSuccess();
        }

        [Command("color")]
        [Summary("colorSummary")]
        [Example("#ff983e")]
        public async Task<RuntimeResult> Color([Summary("colorParam1")] string color = null)
        {
            System.Drawing.Color _color;
            if (string.IsNullOrWhiteSpace(color))
            {
                _color = System.Drawing.Color.FromArgb(RngInstance.Next(0, 256), RngInstance.Next(0, 256), RngInstance.Next(0, 256));
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: Generated random color: {_color}"));
            }
            else
            {
                color = color.TrimStart('#');
                if (!int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rawColor))
                {
                    if (!int.TryParse(color, NumberStyles.Integer, CultureInfo.InvariantCulture, out rawColor))
                    {
                        rawColor = System.Drawing.Color.FromName(color).ToArgb();
                        if (rawColor == 0)
                        {
                            rawColor = color.ToColor();
                            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: Converted string to color: {rawColor}"));
                            //rawColor = uint.Parse(color.ToColor(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        }
                        //return FergunResult.FromError(Locate("InvalidColor"));
                    }
                }
                _color = System.Drawing.Color.FromArgb(rawColor);
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: {color.Truncate(30)} -> {rawColor} -> {_color}"));
            }

            using (Bitmap bmp = new Bitmap(500, 500))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.FromArgb(_color.R, _color.G, _color.B));
                }
                using (Stream stream = new MemoryStream())
                {
                    bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, $"{_color}.png");
                }
            }

            return FergunResult.FromSuccess();
        }

        [AlwaysEnabled]
        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [LongRunning]
        [Command("config", RunMode = RunMode.Async)]
        [Summary("configSummary")]
        [Alias("configuration", "settings")]
        public async Task<RuntimeResult> Config()
        {
            var guild = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);

            string listToShow = "";
            string[] configList = Locate("ConfigList").Split(Environment.NewLine, StringSplitOptions.None);
            for (int i = 0; i < configList.Length; i++)
            {
                listToShow += $"**{i + 1}.** {configList[i]}\n";
            }

            IUserMessage message = null;

            string valueList =
                $"{Locate(guild.CaptionbotAutoTranslate)}\n" +
                $"{Locate(guild.AidAutoTranslate)}\n" +
                $"{Locate(guild.TrackSelection)}";

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(Locate("FergunConfig"))
                .WithDescription(Locate("ConfigPrompt"))
                .AddField(Locate("Option"), listToShow, true)
                .AddField(Locate("Value"), valueList, true)
                .WithColor(FergunClient.Config.EmbedColor);

            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(2))
                .AddCallBack(new Emoji("1️⃣"), async (_, reaction) =>
                {
                    guild.CaptionbotAutoTranslate = !guild.CaptionbotAutoTranslate;
                    await HandleReactionAsync(reaction);
                })
                .AddCallBack(new Emoji("2️⃣"), async (_, reaction) =>
                {
                    guild.AidAutoTranslate = !guild.AidAutoTranslate;
                    await HandleReactionAsync(reaction);
                })
                .AddCallBack(new Emoji("3️⃣"), async (_, reaction) =>
                {
                    guild.TrackSelection = !guild.TrackSelection;
                    await HandleReactionAsync(reaction);
                })
                .AddCallBack(new Emoji("❌"), async (_, reaction) =>
                {
                    await message.TryDeleteAsync();
                });

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();

            async Task HandleReactionAsync(SocketReaction reaction)
            {
                FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guild);
                valueList =
                    $"{Locate(guild.CaptionbotAutoTranslate)}\n" +
                    $"{Locate(guild.AidAutoTranslate)}\n" +
                    $"{Locate(guild.TrackSelection)}";

                builder.Fields[1] = new EmbedFieldBuilder { Name = Locate("Value"), Value = valueList, IsInline = true };
                _ = message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                await message.ModifyAsync(x => x.Embed = builder.Build());
            }
        }

        [LongRunning]
        [Command("define", RunMode = RunMode.Async)]
        [Summary("defineSummary")]
        [Alias("def", "definition", "dictionary")]
        public async Task<RuntimeResult> Define([Remainder, Summary("defineParam1")] string word)
        {
            IReadOnlyList<DefinitionCategory> results;
            try
            {
                results = await DictionaryApi.GetDefinitionsAsync(word, GetLanguage(), true);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling Dictionary API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling Dictionary API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }
            catch (JsonReaderException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error deserializing Dictionary API response", e));
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            if (results == null || results.Count == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            var definitions = new List<SimpleDefinitionInfo>();
            foreach (var result in results)
            {
                foreach (var meaning in result.Meanings)
                {
                    foreach (var definition in meaning.Definitions)
                    {
                        definitions.Add(new SimpleDefinitionInfo
                        {
                            Word = result.Word,
                            PartOfSpeech = meaning.PartOfSpeech,
                            Definition = definition.Definition,
                            Example = definition.Example,
                            Synonyms = definition.Synonyms,
                            Antonyms = definition.Antonyms
                        });
                    }
                }
            }

            var pages = definitions.Select(x =>
            {
                var fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = Locate("Word"),
                        Value =  x.PartOfSpeech == null || x.PartOfSpeech == "undefined" ? x.Word : $"{x.Word} ({x.PartOfSpeech})",
                    },
                    new EmbedFieldBuilder
                    {
                        Name = Locate("Definition"),
                        Value = x.Definition
                    }
                };
                if (!string.IsNullOrEmpty(x.Example))
                {
                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = Locate("Example"),
                        Value = x.Example
                    });
                }
                if (x.Synonyms.Count > 0)
                {
                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = Locate("Synonyms"),
                        Value = string.Join(", ", x.Synonyms)
                    });
                }
                if (x.Antonyms.Count > 0)
                {
                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = Locate("Antonyms"),
                        Value = string.Join(", ", x.Antonyms)
                    });
                }

                return new EmbedBuilder { Fields = fields };
            });

            var pager = new PaginatedMessage
            {
                Title = "Define",
                Pages = pages,
                Color = new Discord.Color(FergunClient.Config.EmbedColor),
                Options = new PaginatorAppearanceOptions
                {
                    FooterFormat = Locate("PaginatorFooter")
                }
            };

            var reactions = new ReactionList
            {
                First = definitions.Count >= 3,
                Backward = true,
                Forward = true,
                Last = definitions.Count >= 3,
                Stop = true,
                Jump = definitions.Count >= 4,
                Info = false
            };

            await PagedReplyAsync(pager, reactions);

            return FergunResult.FromSuccess();
        }

        [Command("editsnipe", RunMode = RunMode.Async)]
        [Summary("editsnipeSummary")]
        [Alias("esnipe")]
        [Example("#bots")]
        public async Task Editsnipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var message = FergunClient.MessageCache
                .Where(x => x.SourceEvent == SourceEvent.MessageUpdated && x.Channel.Id == channel.Id)
                .OrderByDescending(x => x.CachedAt)
                .FirstOrDefault();

            var builder = new EmbedBuilder();
            if (message == null)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                builder.WithAuthor(message.Author)
                    .WithDescription(message.Content.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{message.Channel.Name}")
                    .WithTimestamp(message.CreatedAt);
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [AlwaysEnabled]
        [Command("help")]
        [Summary("helpSummary")]
        [Alias("ayuda", "yardım")]
        [Example("help")]
        public async Task<RuntimeResult> Help([Remainder, Summary("helpParam1")] string commandName = null)
        {
            var builder = new EmbedBuilder();
            if (commandName == null)
            {
                var textCommands = _cmdService.Commands.Where(x => x.Module.Name == "Text").Select(x => x.Name);
                var utilityCommands = _cmdService.Commands.Where(x => x.Module.Name == "Utility").Select(x => x.Name);
                var moderationCommands = _cmdService.Commands.Where(x => x.Module.Name == "Moderation").Select(x => x.Name);
                var musicCommands = _cmdService.Commands.Where(x => x.Module.Name == "Music").Select(x => x.Name);
                var aidCommands = _cmdService.Commands.Where(x => x.Module.Name == "AIDungeon").Select(x => x.Name);
                var otherCommands = _cmdService.Commands.Where(x => x.Module.Name == "Other").Select(x => x.Name);
                var ownerCommands = _cmdService.Commands.Where(x => x.Module.Name == "Owner").Select(x => x.Name);
                int visibleCommandCount = _cmdService.Commands.Count(x => x.Module.Name != Constants.DevelopmentModuleName);

                builder.WithTitle(Locate("CommandList"))
                    .AddField(Locate("TextCommands"), string.Join(", ", textCommands))
                    .AddField(Locate("UtilityCommands"), string.Join(", ", utilityCommands))
                    .AddField(Locate("ModerationCommands"), string.Join(", ", moderationCommands))
                    .AddField(Locate("MusicCommands"), string.Join(", ", musicCommands))
                    .AddField(string.Format(Locate("AIDCommands"), GetPrefix()), string.Join(", ", aidCommands))
                    .AddField(Locate("OtherCommands"), string.Join(", ", otherCommands))
                    .AddField(Locate("OwnerCommands"), string.Join(", ", ownerCommands))
                    .AddField(Locate("Notes"), string.Format(Locate("NotesInfo"), GetPrefix()));

                string version = $"v{Constants.Version}";
                if (FergunClient.IsDebugMode)
                {
                    version += "-dev";
                }
                else
                {
                    builder.AddField("Links", CommandUtils.BuildLinks(Context.Channel));
                }
                builder.WithFooter(string.Format(Locate("HelpFooter"), version, visibleCommandCount))
                    .WithColor(FergunClient.Config.EmbedColor);

                await ReplyAsync(embed: builder.Build());
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Help: Getting help for command: {commandName}"));
                var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != Constants.DevelopmentModuleName);
                if (command == null)
                {
                    return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
                }
                var embed = command.ToHelpEmbed(GetLanguage(), GetPrefix());
                await ReplyAsync(embed: embed);
            }
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("identify", RunMode = RunMode.Async)]
        [Summary("identifySummary")]
        [Alias("captionbot")]
        [Remarks("NoUrlPassed")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Identify([Summary("identifyParam1")] string url = null)
        {
            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(Constants.ClientConfig.MessageCacheSize, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), Constants.ClientConfig.MessageCacheSize));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Identify: url to use: {url}"));

            var data = new Dictionary<string, string>
            {
                { "Content", url },
                { "Type", "CaptionRequest" }
            };

            string text;
            try
            {
                using (var content = new FormUrlEncodedContent(data))
                {
                    var response = await _httpClient.PostAsync(new Uri("https://captionbot.azurewebsites.net/api/messages?language=en-US"), content);
                    response.EnsureSuccessStatusCode();
                    text = await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling CaptionBot API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling CaptionBot API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            text = text.Trim('\"');

            bool autoTranslate = GetGuildConfig()?.CaptionbotAutoTranslate ?? Constants.CaptionbotAutoTranslateDefault;
            if (autoTranslate && GetLanguage() != "en")
            {
                var translation = await TranslateSimpleAsync(text, GetLanguage(), "en");
                if (translation.Error == null)
                {
                    text = translation.Text;
                }
            }

            await ReplyAsync(text);
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("img", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("imgSummary")]
        [Alias("im", "image", "ddgi")]
        [Example("discord")]
        public async Task<RuntimeResult> Img([Remainder, Summary("imgParam1")] string query)
        {
            query = query.Replace("!", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrEmpty(query))
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }
            if (query.Length > DdgApi.MaxLength)
            {
                return FergunResult.FromError(string.Format(Locate("MustBeLowerThan"), nameof(query), DdgApi.MaxLength));
            }

            // Considering a DM channel a SFW channel.
            bool isNsfwChannel = !Context.IsPrivate && (Context.Channel as ITextChannel).IsNsfw;
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Img: Query \"{query}\", NSFW channel: {isNsfwChannel}"));
            DdgResponse search;
            try
            {
                search = await DdgApi.SearchImagesAsync(query, !isNsfwChannel ? SafeSearch.Strict : SafeSearch.Off);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TokenNotFoundException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images", e));
                return FergunResult.FromError(Locate("NoResultsFound"));
            }
            if (search.Results.Count == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Results count: {search.Results.Count}"));

            var pages = search.Results
                .Where(x =>
                Uri.IsWellFormedUriString(Uri.EscapeUriString(Uri.UnescapeDataString(x.Image)), UriKind.Absolute) &&
                Uri.IsWellFormedUriString(x.Url, UriKind.Absolute))
                .Select(x => new EmbedBuilder()
                {
                    Title = x.Title.Truncate(EmbedBuilder.MaxTitleLength),
                    ImageUrl = Uri.EscapeUriString(Uri.UnescapeDataString(x.Image)),
                    Url = x.Url
                })
                .ToList();

            var pager = new PaginatedMessage()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.ToString(),
                    IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                },
                Description = Locate("ImageSearch"),
                Pages = pages,
                Color = new Discord.Color(FergunClient.Config.EmbedColor),
                Options = new PaginatorAppearanceOptions()
                {
                    InformationText = Locate("PaginatorHelp"),
                    FooterFormat = Locate("PaginatorFooter"),
                    Timeout = TimeSpan.FromMinutes(10),
                    ActionOnTimeout = ActionOnTimeout.DeleteReactions
                }
            };

            var reactions = new ReactionList()
            {
                First = pages.Count >= 3,
                Backward = true,
                Forward = true,
                Last = pages.Count >= 3,
                Stop = true,
                Jump = pages.Count >= 4,
                Info = false
            };

            await PagedReplyAsync(pager, reactions);

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [Command("invert", RunMode = RunMode.Async), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("invertSummary")]
        [Alias("negate", "negative")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Invert([Remainder, Summary("invertParam1")] string url = null)
        {
            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(Constants.ClientConfig.MessageCacheSize, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), Constants.ClientConfig.MessageCacheSize));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Invert: url to use: {url}"));

            Stream response;
            try
            {
                response = await _httpClient.GetStreamAsync(new Uri(url));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            using (Bitmap img = new Bitmap(response))
            using (Bitmap inverted = img.InvertColor())
            using (Stream invertedFile = new MemoryStream())
            {
                System.Drawing.Imaging.ImageFormat format = inverted.RawFormat;
                if (inverted.RawFormat.Guid == System.Drawing.Imaging.ImageFormat.MemoryBmp.Guid)
                {
                    format = System.Drawing.Imaging.ImageFormat.Jpeg;
                }
                inverted.Save(invertedFile, format);
                if (invertedFile.Length > Constants.AttachmentSizeLimit)
                {
                    return FergunResult.FromError("The file is too large.");
                }
                invertedFile.Position = 0;
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, invertedFile, $"invert{format.FileExtensionFromEncoder()}");
            }

            return FergunResult.FromSuccess();
        }

        [Command("lmgtfy", RunMode = RunMode.Async)]
        [Summary("lmgtfySummary")]
        public async Task Google([Remainder, Summary("lmgtfyParam1")] string query)
        {
            await ReplyAsync($"https://lmgtfy.com/?q={Uri.EscapeDataString(query)}", allowedMentions: AllowedMentions.None);
        }

        [LongRunning]
        [Command("ocr", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrSummary")]
        [Remarks("NoUrlPassed")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Ocr([Summary("ocrParam1")] string url = null)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.OCRSpaceApiKey))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.OCRSpaceApiKey)));
            }

            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(Constants.ClientConfig.MessageCacheSize, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), Constants.ClientConfig.MessageCacheSize));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Ocr: url to use: {url}"));

            (string error, string text) = await OcrSimpleAsync(url);
            if (!int.TryParse(error, out int processTime))
            {
                return FergunResult.FromError(Locate(error));
            }

            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var response = await Hastebin.UploadAsync(text);
                    text = Format.Url(Locate("HastebinLink"), response.GetLink());
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocr: Error while uploading text to Hastebin", e));
                    text = Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                text = Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            var builder = new EmbedBuilder()
                    .WithTitle(Locate("OcrResults"))
                    .AddField(Locate("Output"), text)
                    .WithFooter(string.Format(Locate("ProcessingTime"), processTime))
                    .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("ocrtranslate", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrtranslateSummary")]
        [Alias("ocrtr")]
        [Remarks("NoUrlPassed")]
        [Example("en https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Ocrtr([Summary("ocrtranslateParam1")] string target,
            [Summary("ocrtranslateParam2")] string url = null)
        {
            if (!GoogleTranslator.IsLanguageSupported(new Language("", target)))
            {
                return FergunResult.FromError($"{Locate("InvalidLanguage")}\n{string.Join(" ", BingTranslatorApi.SupportedLanguages.Select(x => Format.Code(x)))}");
            }

            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(Constants.ClientConfig.MessageCacheSize, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), Constants.ClientConfig.MessageCacheSize));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Orctranslate: url to use: {url}"));

            (string error, string text) = await OcrSimpleAsync(url);
            if (!int.TryParse(error, out int processTime))
            {
                return FergunResult.FromError(Locate(error));
            }

            var sw = Stopwatch.StartNew();
            var translation = await TranslateSimpleAsync(text, target);
            sw.Stop();
            if (translation.Error != null)
            {
                return FergunResult.FromError(Locate(translation.Error));
            }

            if (translation.Text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var response = await Hastebin.UploadAsync(translation.Text);
                    translation.Text = Format.Url(Locate("HastebinLink"), response.GetLink());
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocrtranslate: Error while uploading text to Hastebin", e));
                    translation.Text = Format.Code(translation.Text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                translation.Text = Format.Code(translation.Text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("OcrtrResults"))
                .AddField(Locate("Input"), Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("SourceLanguage"), translation.Source?.FullName ?? "???", false)
                .AddField(Locate("TargetLanguage"), translation.Target.FullName, false)
                .AddField(Locate("Result"), translation.Text, false)
                .WithFooter(string.Format(Locate("ProcessingTime"), processTime + sw.ElapsedMilliseconds))
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [Command("paste", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("pasteSummary")]
        [Alias("haste")]
        public async Task<RuntimeResult> Paste([Remainder, Summary("pasteParam1")] string text)
        {
            await SendEmbedAsync($"{FergunClient.Config.LoadingEmote} {Locate("Uploading")}");
            try
            {
                await SendEmbedAsync(Format.Url(Locate("HastebinLink"), (await Hastebin.UploadAsync(text)).GetLink()));
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Paste: Error while uploading text to Hastebin", e));
                return FergunResult.FromError(e.Message);
            }
            return FergunResult.FromSuccess();
        }

        [Command("ping")]
        [Summary("pingSummary")]
        public async Task Ping()
        {
            var sw = Stopwatch.StartNew();
            await SendEmbedAsync(Format.Bold("Pong!"));
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            FergunClient.Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, _ => true);
            sw2.Stop();

            await SendEmbedAsync($"⏱{Format.Bold("Message")}: {sw.ElapsedMilliseconds}ms\n\n" +
                $"{FergunClient.Config.WebSocketEmote}{Format.Bold("WebSocket")}: {Context.Client.Latency}ms\n\n" +
                $"{FergunClient.Config.MongoDbEmote}{Format.Bold("Database")}: {Math.Round(sw2.Elapsed.TotalMilliseconds, 2)}ms");
        }

        [LongRunning]
        [Command("resize", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("resizeSummary")]
        [Alias("waifu2x", "w2x")]
        [Remarks("NoUrlPassed")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Resize([Summary("resizeParam1")] string url = null)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.DeepAiApiKey))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.DeepAiApiKey)));
            }

            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(Constants.ClientConfig.MessageCacheSize, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), Constants.ClientConfig.MessageCacheSize));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Resize: url to use: {url}"));

            string json;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepai.org/api/waifu2x"))
                {
                    request.Headers.Add("Api-Key", FergunClient.Config.DeepAiApiKey);
                    request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("image", url) });
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling waifu2x API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling waifu2x API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            dynamic obj = JsonConvert.DeserializeObject(json);
            string resultUrl = (string)obj.output_url;
            if (string.IsNullOrWhiteSpace(resultUrl))
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ResizeResults"))
                .WithImageUrl(resultUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("roleinfo")]
        [Summary("roleinfoSummary")]
        [Alias("role")]
        [Example("Devs")]
        public async Task<RuntimeResult> Roleinfo([Remainder, Summary("roleinfoParam1")] SocketRole role)
        {
            int memberCount = role.Members.Count();

            var builder = new EmbedBuilder()
                .WithTitle(Locate("RoleInfo"))

                .AddField(Locate("Name"), role.Name, true)
                .AddField(Locate("Color"), $"{role.Color} ({role.Color.R}, {role.Color.G}, {role.Color.B})", true)
                .AddField(Locate("IsMentionable"), Locate(role.IsMentionable), true)

                .AddField("ID", role.Id, true)
                .AddField(Locate("IsHoisted"), Locate(role.IsHoisted), true)
                .AddField(Locate("Position"), role.Position, true)

                .AddField(Locate("Permissions"), role.Permissions.RawValue == 0 ? Locate("None") : Format.Code(string.Join("`, `", role.Permissions.ToList())), false)

                .AddField(Locate("MemberCount"), Context.Guild.HasAllMembers ? memberCount.ToString() : memberCount == 0 ? "?" : "~" + memberCount, true)
                .AddField(Locate("CreatedAt"), role.CreatedAt, true)
                .AddField(Locate("Mention"), role.Mention, true)

                .WithColor(role.Color);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        // The attribute order matters
        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("screenshot", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("screenshotSummary")]
        [Alias("ss")]
        [Example("https://www.fergun.com")]
        public async Task<RuntimeResult> Screenshot([Summary("screenshotParam1")] string url)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.ApiFlashAccessKey))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.ApiFlashAccessKey)));
            }

            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Screenshot: Invalid url: {Uri.UnescapeDataString(url)}"));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Screenshot: Url: {uri.AbsoluteUri}"));

            ApiFlashResponse response;
            try
            {
                response = await ApiFlash.UrlToImageAsync(FergunClient.Config.ApiFlashAccessKey, uri.AbsoluteUri, ApiFlash.FormatType.png, "400,403,404,500-511");
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Screenshot: Error in API", e));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            catch (WebException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Screenshot: Error in API", e));
                return FergunResult.FromError(e.Message);
            }

            if (response.ErrorMessage != null)
            {
                return FergunResult.FromError(response.ErrorMessage);
            }

            try
            {
                using (Stream image = await _httpClient.GetStreamAsync(new Uri(response.Url)))
                {
                    await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, image, "screenshot.png");
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            return FergunResult.FromSuccess();
        }

        [Command("serverinfo", RunMode = RunMode.Async)]
        [Summary("serverinfoSummary")]
        [Alias("server", "guild", "guildinfo")]
        public async Task<RuntimeResult> Serverinfo(string serverId = null)
        {
            SocketGuild server;
            if (Context.User.Id == (await Context.Client.GetApplicationInfoAsync()).Owner.Id)
            {
                server = serverId == null ? Context.Guild : Context.Client.GetGuild(ulong.Parse(serverId));
                if (server == null)
                {
                    return FergunResult.FromError(Locate("GuildNotFound"));
                }
            }
            else
            {
                server = Context.Guild;
            }

            string channelCountInfo = $"{server.TextChannels.Count + server.VoiceChannels.Count} " +
                $"({FergunClient.Config.TextEmote} {server.TextChannels.Count} **|** " +
                $"{FergunClient.Config.VoiceEmote} {server.VoiceChannels.Count})";

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ServerInfo"))

                .AddField(Locate("Name"), server.Name, true)
                .AddField(Locate("Owner"), MentionUtils.MentionUser(server.OwnerId), true)
                .AddField("ID", server.Id, true)

                .AddField(Locate("CategoryCount"), server.CategoryChannels.Count, true)
                .AddField(Locate("ChannelCount"), channelCountInfo, true)
                .AddField(Locate("RoleCount"), server.Roles.Count, true)

                .AddField(Locate("DefaultChannel"), server.DefaultChannel?.Mention ?? Locate("None"), true)
                .AddField(Locate("Region"), Format.Code(server.VoiceRegionId), true)
                .AddField(Locate("VerificationLevel"), Locate(server.VerificationLevel.ToString()), true)

                .AddField(Locate("BoostTier"), (int)server.PremiumTier, true)
                .AddField(Locate("BoostCount"), server.PremiumSubscriptionCount, true)
                .AddField(Locate("ServerFeatures"), server.Features.Count == 0 ? Locate("None") : string.Join(", ", server.Features), true);

            if (server.HasAllMembers)
            {
                builder.AddField(Locate("Members"), $"{server.MemberCount} (Bots: {server.Users.Count(x => x.IsBot)}) **|** " +
                $"{FergunClient.Config.OnlineEmote} {server.Users.Count(x => x.Status == UserStatus.Online)} **|** " +
                $"{FergunClient.Config.IdleEmote} {server.Users.Count(x => x.Status == UserStatus.Idle)} **|** " +
                $"{FergunClient.Config.DndEmote} {server.Users.Count(x => x.Status == UserStatus.DoNotDisturb)} **|** " +
                $"{FergunClient.Config.StreamingEmote} {server.Users.Count(x => x.Activities.Any(x => x.Type == ActivityType.Streaming))} **|** " +
                $"{FergunClient.Config.OfflineEmote} {server.Users.Count(x => x.Status == UserStatus.Offline)}");
            }
            else
            {
                builder.AddField(Locate("Members"), server.MemberCount, true);
            }

            //if (server.Emotes.Count == 0)
            //{
            //    builder.AddField("Emotes", Locate("None"), false);
            //}
            //else
            //{
            //    var chunks = string.Join(" ", server.Emotes.Select(x => x.ToString()))
            //        .SplitToLines(EmbedFieldBuilder.MaxFieldValueLength)
            //        .ToList();
            //    for (int i = 0; i < chunks.Count; i++)
            //    {
            //        builder.AddField(i == 0 ? "Emotes" : "\u200B", chunks[i], false);
            //    }
            //}
            //builder.AddField("Emotes: ", server.Emotes.Any() ? string.Join(" ", server.Emotes.ToList()).Truncate(1021) + "..." : "(None)", false);
            builder.AddField(Locate("CreatedAt"), server.CreatedAt, true);
            builder.WithThumbnailUrl(server.Features.Any(x => x == "ANIMATED_ICON") ? Path.ChangeExtension(server.IconUrl, "gif") : server.IconUrl);
            //if (server.Features.Any(x => x == "BANNER"))
            //{
            //}
            builder.WithColor(FergunClient.Config.EmbedColor);
            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("snipe", RunMode = RunMode.Async)]
        [Summary("snipeSummary")]
        [Example("#help")]
        public async Task Snipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var message = FergunClient.MessageCache
                .Where(x => x.SourceEvent == SourceEvent.MessageDeleted && x.Channel.Id == channel.Id)
                .OrderByDescending(x => x.CachedAt)
                .FirstOrDefault();

            var builder = new EmbedBuilder();
            if (message == null)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                string text = !string.IsNullOrEmpty(message.Content) ? message.Content : message.Attachments.Count > 0 ? $"({Locate("Attachment")})" : "?";

                builder.WithAuthor(message.Author)
                    .WithDescription(text.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{message.Channel.Name}")
                    .WithTimestamp(message.CreatedAt);
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [LongRunning]
        [Command("translate", RunMode = RunMode.Async)]
        [Summary("translateSummary")]
        [Alias("tr")]
        [Example("es hello world")]
        public async Task<RuntimeResult> Translate([Summary("translateParam1")] string target,
            [Remainder, Summary("translateParam2")] string text)
        {
            if (!GoogleTranslator.IsLanguageSupported(new Language("", target)))
            {
                return FergunResult.FromError($"{Locate("InvalidLanguage")}\n{string.Join(" ", BingTranslatorApi.SupportedLanguages.Select(x => Format.Code(x)))}");
            }

            var result = await TranslateSimpleAsync(text, target);
            if (result.Error != null)
            {
                return FergunResult.FromError(Locate(result.Error));
            }

            if (result.Text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var response = await Hastebin.UploadAsync(result.Text);
                    result.Text = Format.Url(Locate("HastebinLink"), response.GetLink());
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Translate: Error while uploading text to Hastebin", e));
                    result.Text = Format.Code(result.Text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                result.Text = Format.Code(result.Text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("TranslationResults"))
                .AddField(Locate("SourceLanguage"), result.Source?.FullName ?? "???", false)
                .AddField(Locate("TargetLanguage"), result.Target.FullName, false)
                .AddField(Locate("Result"), result.Text, false)
                .WithThumbnailUrl("https://fergun.is-inside.me/u7fSdkx8.png")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("tts", RunMode = RunMode.Async), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("Text to speech.")]
        [Alias("texttospeech", "t2s")]
        [Example("en hello world")]
        public async Task<RuntimeResult> Tts([Summary("ttsParam1")] string target,
            [Remainder, Summary("ttsParam2")] string text)
        {
            target = target.ToLowerInvariant();
            text = text.ToLowerInvariant();

            if (!GoogleTranslator.IsLanguageSupported(new Language("", target)))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"TTS: Target language not supported ({target})"));
                //return CustomResult.FromError(GetValue("InvalidLanguage"));
                text = $"{target} {text}";
                target = "en";
            }

            byte[] bytes;
            try
            {
                bytes = await GoogleTTS.GetTtsAsync(text, target);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "TTS: Error while getting TTS", e));
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "TTS: Error while getting TTS", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            using (var stream = new MemoryStream(bytes))
            {
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, "tts.mp3");
            }

            return FergunResult.FromSuccess();
        }

        // The attribute order matters
        [LongRunning]
        [Command("urban", RunMode = RunMode.Async)]
        [Summary("urbanSummary")]
        [Alias("ud")]
        [Example("pog")]
        public async Task<RuntimeResult> Urban([Remainder, Summary("urbanParam1")] string query = null)
        {
            UrbanResponse search = null;

            query = query?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Urban: Getting random words..."));
                try
                {
                    search = UrbanApi.GetRandomWords();
                }
                catch (WebException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Urban: Error in API", e));
                    return FergunResult.FromError($"Error in Urban Dictionary API: {e.Message}");
                }
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Urban: Query \"{query}\""));
                try
                {
                    search = UrbanApi.SearchWord(query);
                }
                catch (WebException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Urban: Error in API", e));
                    return FergunResult.FromError($"Error in Urban Dictionary API: {e.Message}");
                }
                if (search.Definitions.Count == 0)
                {
                    return FergunResult.FromError(Locate("NoResults"));
                }
            }

            var pages = search.Definitions.Select(x =>
            {
                // Replace all occurrences of a term in brackets with a hyperlink directing to the definition of that term.
                string definition = _bracketRegex.Replace(x.Definition,
                    m => Format.Url(m.Groups[1].Value, $"https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)}"))
                .Truncate(EmbedBuilder.MaxDescriptionLength);

                string example;
                if (string.IsNullOrEmpty(x.Example))
                {
                    example = Locate("NoExample");
                }
                else
                {
                    example = _bracketRegex.Replace(x.Example,
                        m => Format.Url(m.Groups[1].Value, $"https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)}"))
                    .Truncate(EmbedFieldBuilder.MaxFieldValueLength);
                }

                var fields = new List<EmbedFieldBuilder>()
                {
                    new EmbedFieldBuilder()
                    {
                        Name = Locate("Example"),
                        Value = example,
                        IsInline = false
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "👍",
                        Value = x.ThumbsUp,
                        IsInline = true
                    },
                    new EmbedFieldBuilder()
                    {
                        Name = "👎",
                        Value = x.ThumbsDown,
                        IsInline = true
                    }
                };

                return new EmbedBuilder()
                {
                    Author = new EmbedAuthorBuilder { Name = $"{Locate("By")} {x.Author}" },
                    Title = x.Word.Truncate(EmbedBuilder.MaxTitleLength),
                    Description = definition,
                    Url = x.Permalink,
                    Fields = fields,
                    Timestamp = x.WrittenOn
                };
            });

            var pager = new PaginatedMessage()
            {
                Pages = pages,
                Color = new Discord.Color(FergunClient.Config.EmbedColor),
                Options = new PaginatorAppearanceOptions()
                {
                    FooterFormat = $"Urban Dictionary {(string.IsNullOrWhiteSpace(query) ? "(Random words)" : "")} - {Locate("PaginatorFooter")}",
                    Timeout = TimeSpan.FromMinutes(10),
                    ActionOnTimeout = ActionOnTimeout.DeleteReactions
                }
            };

            var reactions = new ReactionList()
            {
                First = search.Definitions.Count >= 3,
                Backward = true,
                Forward = true,
                Last = search.Definitions.Count >= 3,
                Stop = true,
                Jump = search.Definitions.Count >= 4,
                Info = false
            };

            await PagedReplyAsync(pager, reactions);

            return FergunResult.FromSuccess();
        }

        [Command("userinfo", RunMode = RunMode.Async)]
        [Summary("userinfoSummary")]
        [Alias("ui", "user", "whois")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> Userinfo([Remainder, Summary("userinfoParam1")] IUser user = null)
        {
            user ??= Context.User;

            string activities = Locate("None");
            if (user.Activities.Count > 0)
            {
                activities = string.Join('\n', user.Activities.Select(x =>
                x.Type == ActivityType.CustomStatus ?
                (x as CustomStatusGame).ToString() :
                $"{x.Type} {x.Name}"));
            }

            string clients = "?";
            if (user.ActiveClients.Count > 0)
            {
                clients = string.Join(' ', user.ActiveClients.Select(x =>
                x == ClientType.Desktop ? "🖥" :
                x == ClientType.Mobile ? "📱" :
                x == ClientType.Web ? "🌐" : ""));
            }

            var guildUser = user as IGuildUser;

            if (!(user is RestUser))
            {
                // Prevent getting error 404 while downloading the avatar getting the user from REST.
                user = await Context.Client.Rest.GetUserAsync(user.Id);
            }

            string avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();
            string thumbnail = user.GetAvatarUrl(ImageFormat.Png, 128) ?? user.GetDefaultAvatarUrl();

            System.Drawing.Color avatarColor;
            try
            {
                using (Stream response = await _httpClient.GetStreamAsync(new Uri(thumbnail)))
                using (Bitmap img = new Bitmap(response))
                {
                    avatarColor = img.GetAverageColor();
                }
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("UserInfo"))
                .AddField(Locate("Name"), user.ToString(), false)
                .AddField("Nickname", guildUser?.Nickname ?? Locate("None"), false)
                .AddField("ID", user.Id, false)
                .AddField(Locate("Activity"), activities, true)
                .AddField(Locate("ActiveClients"), clients, true)
                .AddField(Locate("IsBot"), Locate(user.IsBot), false)
                .AddField(Locate("CreatedAt"), user.CreatedAt)
                .AddField(Locate("GuildJoinDate"), guildUser?.JoinedAt?.ToString() ?? "N/A")
                .AddField(Locate("BoostingSince"), guildUser?.PremiumSince?.ToString() ?? "N/A")
                .WithThumbnailUrl(avatarUrl)
                .WithColor(new Discord.Color(avatarColor.R, avatarColor.G, avatarColor.B));

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("wikipedia", RunMode = RunMode.Async)]
        [Summary("wikipediaSummary")]
        [Alias("wiki")]
        [Example("Discord")]
        public async Task<RuntimeResult> Wikipedia([Remainder, Summary("wikipediaParam1")] string query)
        {
            string response;
            using (WebClient wc = new WebClient())
            {
                // I would want to know who did the awful json response structure.
                response = await wc.DownloadStringTaskAsync($"https://{GetLanguage()}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&format=json");
            }
            //response = '[' + SearchResponse.Substring(SearchResponse.IndexOf(',') + 1, SearchResponse.Length - SearchResponse.IndexOf(',') - 1);
            List<dynamic> search = JsonConvert.DeserializeObject<List<dynamic>>(response);
            string langToUse = GetLanguage();
            if (search[1].Count == 0)
            {
                if (langToUse == "en")
                {
                    return FergunResult.FromError(Locate("NoResults"));
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Wikipedia: No results found on non-English Wikipedia. Searching on English Wikipedia."));
                    langToUse = "en";
                    using (WebClient wc = new WebClient())
                    {
                        response = await wc.DownloadStringTaskAsync($"https://{langToUse}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&format=json");
                    }
                    search = JsonConvert.DeserializeObject<List<dynamic>>(response);
                    if (search[1].Count == 0)
                    {
                        return FergunResult.FromError(Locate("NoResults"));
                    }
                }
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Wikipedia: Results count: {search[1].Count}"));

            try
            {
                using (WebClient wc = new WebClient())
                {
                    string articleUrl = search[search.Count - 1][0];
                    string apiUrl = $"https://{langToUse}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(Uri.UnescapeDataString(articleUrl.Substring(30)))}";
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Wikipedia: Downloading article from url: {apiUrl}"));

                    response = await wc.DownloadStringTaskAsync(apiUrl);
                }
            }
            catch (WebException e)
            {
                return FergunResult.FromError(e.Message);
            }

            var article = JsonConvert.DeserializeObject<WikiArticle>(response);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(article.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithDescription(article.Extract.Truncate(EmbedBuilder.MaxDescriptionLength))
                .WithFooter(Locate("WikipediaSearch"))
                .WithThumbnailUrl("https://upload.wikimedia.org/wikipedia/commons/6/63/Wikipedia-logo.png")
                .WithColor(FergunClient.Config.EmbedColor);

            string url = Context.User.ActiveClients.Any(x => x == ClientType.Mobile) ? article.ContentUrls.Mobile.Page : article.ContentUrls.Desktop.Page;
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                builder.WithUrl(url);
            }
            else
            {
                url = WebUtility.UrlDecode(url);
                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    builder.WithUrl(url);
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Wikipedia: Invalid url to article: {url}"));
                }
            }

            if (!Context.IsPrivate && (Context.Channel as ITextChannel).IsNsfw && article.Originalimage?.Source != null)
            {
                string decodedUrl = Uri.UnescapeDataString(article.Originalimage.Source);
                if (Uri.IsWellFormedUriString(decodedUrl, UriKind.Absolute))
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Wikipedia: Using image Url: {decodedUrl}"));
                    builder.ThumbnailUrl = decodedUrl;
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Wikipedia: Invalid image url: {decodedUrl}"));
                }
            }

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("wolframalpha", RunMode = RunMode.Async)]
        [Summary("wolframalphaSummary")]
        [Alias("wolfram", "wa")]
        [Example("2 + 2")]
        public async Task<RuntimeResult> Wolframalpha([Remainder, Summary("wolframalphaParam1")] string query)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.WolframAlphaAppId))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.WolframAlphaAppId)));
            }

            string result;
            try
            {
                result = await _httpClient.GetStringAsync(new Uri($"https://api.wolframalpha.com/v1/result?i={Uri.EscapeDataString(query)}&appid={FergunClient.Config.WolframAlphaAppId}"));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling WolframAlpha API", e));
                return FergunResult.FromError(e.Message);
            }

            var builder = new EmbedBuilder()
                .WithAuthor("Wolfram Alpha", "https://fergun.is-inside.me/W3gbxqzY.png")
                .AddField(Locate("Input"), Format.Code(query.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("Output"), Format.Code(result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .WithFooter("wolframalpha.com")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("xkcd")]
        [Summary("xkcdSummary")]
        [Example("1000")]
        public async Task<RuntimeResult> Xkcd([Summary("xkcdParam1")] int? number = null)
        {
            UpdateLastComic();
            if (_lastComic == null)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            if (number != null && (number < 1 || number > _lastComic.Num))
            {
                return FergunResult.FromError(string.Format(Locate("InvalidxkcdNumber"), _lastComic.Num));
            }
            if (number == 404)
            {
                return FergunResult.FromError("404 Not Found");
            }
            string response;
            using (WebClient wc = new WebClient())
            {
                response = await wc.DownloadStringTaskAsync($"https://xkcd.com/{number ?? RngInstance.Next(1, _lastComic.Num)}/info.0.json");
            }

            XkcdComic comic = JsonConvert.DeserializeObject<XkcdComic>(response);

            var builder = new EmbedBuilder()
                .WithTitle(comic.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl($"https://xkcd.com/{comic.Num}/")
                .WithImageUrl(comic.Img)
                .WithFooter(comic.Alt.Truncate(EmbedFooterBuilder.MaxFooterTextLength))
                .WithTimestamp(new DateTime(int.Parse(comic.Year), int.Parse(comic.Month), int.Parse(comic.Day)));

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("youtube", RunMode = RunMode.Async)]
        [Summary("youtubeSummary")]
        [Alias("yt")]
        public async Task<RuntimeResult> Youtube([Remainder, Summary("youtubeParam1")] string query)
        {
            IReadOnlyList<Video> videos;
            try
            {
                videos = await _ytClient.Search.GetVideosAsync(query, 0, 1);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Youtube: Error obtaining videos (query: {query})", e));
                return FergunResult.FromError(e.Message);
            }
            if (videos.Count == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }
            await ReplyAsync(videos[0].Url);

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("ytrandom", RunMode = RunMode.Async)]
        [Summary("ytrandomSummary")]
        [Alias("ytrand")]
        public async Task<RuntimeResult> Ytrandom()
        {
            for (int i = 0; i < 10; i++)
            {
                string randStr = StringUtils.RandomString(RngInstance.Next(5, 7), RngInstance);
                var items = await _ytClient.Search.GetVideosAsync(randStr).BufferAsync(5);
                if (items.Count != 0)
                {
                    string id = items[RngInstance.Next(items.Count)].Id;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Ytrandom", $"Using id: {id} (random string: {randStr}, search count: {items.Count})"));

                    await ReplyAsync($"https://www.youtube.com/watch?v={id}");
                    return FergunResult.FromSuccess();
                }
                else
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Ytrandom", $"No videos found on random string ({randStr})"));
                }
            }

            return FergunResult.FromError(Locate("AnErrorOccurred"));
        }

        // TODO Use Engine 1 if the image is too small (30x30)
        private static async Task<(string, string)> OcrSimpleAsync(string url)
        {
            if (!Enum.TryParse((await StringUtils.GetUrlMediaTypeAsync(url)).Substring(6), true, out FileType fileType))
            {
                return ("InvalidFileType", null);
            }

            OCREngine engine = fileType == FileType.GIF ? OCREngine.Engine1 : OCREngine.Engine2;

            OCRSpaceResponse ocr;
            try
            {
                ocr = await OCRSpaceApi.PerformOcrFromUrlAsync(FergunClient.Config.OCRSpaceApiKey, url, fileType: fileType, ocrEngine: engine);
            }
            catch (WebException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "OcrSimple", "Error in OCR", e));
                return ("OcrApiError", null);
            }

            if (ocr.IsErroredOnProcessing || ocr.OcrExitCode != 1)
            {
                return (ocr.ParsedResults?.FirstOrDefault()?.ErrorMessage ?? ocr.ErrorMessage[0], null);
            }
            else if (string.IsNullOrWhiteSpace(ocr.ParsedResults[0].ParsedText))
            {
                return ("OcrEmpty", null);
            }

            string text = ocr.ParsedResults[0].ParsedText
                .Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            return (ocr.ProcessingTimeInMilliseconds, text);
        }

        private static async Task<SimpleTranslationResult> TranslateSimpleAsync(string text, string target, string source = "")
        {
            string resultError = null;
            string resultTranslation = null;
            Language resultTarget = GoogleTranslator.GetLanguageByISO(target);
            Language resultSource = null;

            bool useBing = false;
            text = text.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Translator", $"Target: {resultTarget}"));
            try
            {
                var translator = new GoogleTranslator();
                var result = await translator.TranslateLiteAsync(text, string.IsNullOrEmpty(source) ? Language.Auto : new Language("", source), resultTarget);

                resultTranslation = result.MergedTranslation;
                resultSource = result.LanguageDetections[0].Language;

                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Translator", $"Detected language: {resultSource}"));
            }
            catch (Exception e) when (e is GoogleTranslateIPBannedException || e is HttpRequestException || e is TaskCanceledException || e is SystemException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Translator", "Error while translating, using Bing", e));
                useBing = true;
            }
            if (useBing)
            {
                try
                {
                    var result = await BingTranslatorApi.TranslateAsync(text, target);

                    resultTranslation = result[0].Translations[0].Text;
                    resultSource = GoogleTranslator.GetLanguageByISO(result[0].DetectedLanguage.Language);

                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Translator", $"Detected language: {result[0].DetectedLanguage.Language}"));
                }
                catch (Exception e) when (e is JsonSerializationException || e is HttpRequestException || e is TaskCanceledException || e is ArgumentException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Translator", "Error while translating", e));
                    resultError = "ErrorInTranslation";
                }
            }

            return new SimpleTranslationResult
            {
                Error = resultError,
                Text = resultTranslation,
                Source = resultSource,
                Target = resultTarget
            };
        }

        private static void UpdateLastComic()
        {
            if (_timeToCheckComic < DateTimeOffset.UtcNow)
            {
                string response;
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        response = wc.DownloadString("https://xkcd.com/info.0.json");
                    }
                }
                catch (WebException) { return; }
                _lastComic = JsonConvert.DeserializeObject<XkcdComic>(response);
                _timeToCheckComic = DateTimeOffset.UtcNow.AddDays(1);
            }
        }
    }

    public class SimpleTranslationResult
    {
        public string Error { get; set; }
        public Language Source { get; set; }
        public Language Target { get; set; }
        public string Text { get; set; }
    }
}