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
using Discord.Addons.CommandCache;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.APIs;
using Fergun.APIs.DuckDuckGo;
using Fergun.APIs.UrbanDictionary;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Responses;
using GoogleTranslateFreeApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using org.mariuszgromada.math.mxparser;
using YoutubeExplode;
//using Tesseract;

namespace Fergun.Modules
{
    [Ratelimit(3, FergunClient.GlobalCooldown, Measure.Minutes)]
    public class Utility : FergunBase
    {
        [ThreadStatic]
        private static Random _rngInstance;

        private static readonly Regex _bracketReplacer = new Regex(@"\[(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled); // \[(\[*.+?]*)\]
        private static readonly HttpClient _deepAiClient = new HttpClient();
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly YoutubeClient _ytClient = new YoutubeClient();
        private static readonly object _videoCacheLock = new object();
        private static bool _isCreatingCache = false;
        private static XkcdResponse _lastComic = null;
        private static DateTime _timeToCheckComic;
        private static CommandService _cmdService;

        //public static TesseractEngine TessEngine { get; } = new TesseractEngine("./tessdata", "eng", EngineMode.LstmOnly);
        public static List<CachedPages> ImgCache { get; } = new List<CachedPages>();
        private static List<CachedTts> TtsCache { get; } = new List<CachedTts>();
        private static List<CachedPages> UrbanCache { get; } = new List<CachedPages>();
        private static List<string> VideoCache { get; } = new List<string>();
        public static Emote OnlineEmote { get; set; } = Emote.Parse("<:online:726601254016647241>");
        public static Emote IdleEmote { get; set; } = Emote.Parse("<:idle:726601265563566111>");
        public static Emote DndEmote { get; set; } = Emote.Parse("<:dnd:726601274434519090>");
        public static Emote StreamingEmote { get; set; } = Emote.Parse("<:streaming:728358352333045832>");
        public static Emote OfflineEmote { get; set; } = Emote.Parse("<:invisible:726601281455783946>");
        public static Emote TextEmote { get; set; } = Emote.Parse("<:text:728358376278458368>");
        public static Emote VoiceEmote { get; set; } = Emote.Parse("<:voice:728358400316145755>");
        public static Emote MongoDbEmote { get; set; } = Emote.Parse("<:mongodb:728358607195996271>");
        private static Random RngInstance => _rngInstance ??= new Random();

        public Utility(CommandService commands)
        {
            _cmdService ??= commands;
        }

        [Command("avatar")]
        [Summary("avatarSummary")]
        public async Task Avatar([Remainder, Summary("avatarParam1")] IUser user = null)
        {
            user ??= Context.User;

            string avatarUrl = user.GetAvatarUrl(Discord.ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();
            string thumbnail = user.GetAvatarUrl(Discord.ImageFormat.Png, 128) ?? user.GetDefaultAvatarUrl();

            System.Drawing.Color avatarColor;
            using (Stream response = await _httpClient.GetStreamAsync(new Uri(thumbnail)))
            {
                using (Bitmap img = new Bitmap(response))
                {
                    avatarColor = img.GetAverageColor();
                }
            }

            var builder = new EmbedBuilder
            {
                Title = user.ToString(),
                ImageUrl = avatarUrl,
                Color = new Discord.Color(avatarColor.R, avatarColor.G, avatarColor.B)
            };

            await ReplyAsync(null, false, builder.Build());
        }

        [LongRunning]
        [Command("badtranslator", RunMode = RunMode.Async), Ratelimit(1, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("badtranslatorSummary")]
        [Alias("bt")]
        public async Task<RuntimeResult> Badtranslator([Remainder, Summary("badtranslatorParam1")] string text = null)
        {
            var builder = new EmbedBuilder
            {
                Title = "Bad translator"
            };

            if (text == null)
            {
                if (FergunClient.WordList.Count == 0)
                {
                    return FergunResult.FromError("Could not get the word list.");
                }

                // Get random words
                int maxLength = RngInstance.Next(6, 10);
                for (int i = 0; i < maxLength; i++)
                {
                    text += FergunClient.WordList[RngInstance.Next(0, FergunClient.WordList.Count)] + ' ';
                }
                builder.AddField(Locate("Input"), text);
            }
            
            List<string> languageChain = new List<string>();
            int chainCount = 7;
            string originalLang = null;
            string targetLang;
            SimpleTranslationResult result;
            for (int i = 0; i < chainCount; i++)
            {
                // TODO: Skip repeated languages
                // Translate to the original language in the last iteration, otherwise get a random language
                targetLang = i == chainCount - 1 ? originalLang : Translators.SupportedLanguages[RngInstance.Next(0, Translators.SupportedLanguages.Count)];

                result = await TranslateSimpleAsync(text, targetLang, "");
                if (result.Error != null)
                {
                    return FergunResult.FromError(Locate(result.Error));
                }
                if (i == 0)
                {
                    // The detected language fallbacks to English if not found.
                    originalLang = result.Source.ISO639;
                    languageChain.Add(originalLang);
                }
                text = result.Text;
                languageChain.Add(targetLang);
            }

            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                var response = await Hastebin.UploadAsync(text);
                text = Format.Code(Locate("HastebinLink"), response.GetLink());
            }

            builder.AddField(Locate("LanguageChain"), string.Join(" -> ", languageChain))
                .AddField(Locate("Result"), text)
                .WithThumbnailUrl("https://fergun.is-inside.me/gXEDLZVr.png")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(null, false, builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("base64decode")]
        [Summary("base64decodeSummary")]
        [Alias("b64decode", "b64d")]
        public async Task<RuntimeResult> Base64decode([Remainder, Summary("base64decodeParam1")] string text)
        {
            try
            {
                await ReplyAsync(Encoding.UTF8.GetString(Convert.FromBase64String(text)), allowedMentions: AllowedMentions.None);
            }
            catch (FormatException)
            {
                return FergunResult.FromError(Locate("base64decodeInvalid"));
            }
            return FergunResult.FromSuccess();
        }

        [Command("base64encode")]
        [Summary("base64encodeSummary")]
        [Alias("b64encode", "b64e")]
        public async Task Base64encode([Remainder, Summary("base64encodeParam1")] string text)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            if (encoded.Length > DiscordConfig.MaxMessageSize)
            {
                var response = await Hastebin.UploadAsync(encoded);
                await ReplyAsync($"{Hastebin.ApiEndpoint}/{response.Key}");
            }
            else
            {
                await ReplyAsync(encoded);
            }
        }
        [Command("calc", RunMode = RunMode.Async)]
        [Summary("calcSummary")]
        [Alias("calculate")]
        public async Task<RuntimeResult> Calc([Remainder, Summary("calcParam1")] string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }
            string result;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                Expression ex = new Expression(expression);
                result = ex.calculate().ToString();
            }
            catch (Exception e)
            {
                return FergunResult.FromError(e.Message);
            }
            finally
            {
                sw.Stop();
            }
            if (result.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                result = result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 3) + "...";
            }

            var builder = new EmbedBuilder()
                    .WithTitle(Locate("CalcResults"))
                    .AddField(Locate("Input"), $"```{expression}```")
                    .AddField(Locate("Output"), $"```{result}```")
                    .WithFooter(string.Format(Locate("EvalFooter"), sw.ElapsedMilliseconds))
                    .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("channelinfo")]
        [Summary("channelinfoSummary")]
        [Alias("channel")]
        public async Task<RuntimeResult> Channelinfo([Remainder, Summary("channelinfoParam1")] ITextChannel channel = null)
        {
            channel ??= Context.Channel as ITextChannel;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ChannelInfo"));

            builder.AddField(Locate("Name"), channel.Name, true);
            builder.AddField(Locate("Topic"), string.IsNullOrEmpty(channel.Topic) ? Locate("None") : channel.Topic, true); //I use IsNullOrEmpty because topic is "" and not null.
            builder.AddField(Locate("IsNSFW"), Locate(channel.IsNsfw), true);

            builder.AddField("ID", channel.Id, true);
            builder.AddField(Locate("SlowMode"), TimeSpan.FromSeconds(channel.SlowModeInterval).ToShortForm2(), true);
            builder.AddField(Locate("Position"), channel.Position, true);

            builder.AddField(Locate("Category"), channel.CategoryId.HasValue ? Context.Guild.GetCategoryChannel(channel.CategoryId.Value).Name : Locate("None"), true);
            builder.AddField(Locate("CreatedAt"), channel.CreatedAt, true);
            builder.AddField(Locate("Mention"), channel.Mention, true);

            builder.WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("choice")]
        [Summary("choiceSummary")]
        [Alias("choose")]
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
        public async Task<RuntimeResult> Color([Summary("colorParam1")] string color = null)
        {
            System.Drawing.Color _color;
            if (string.IsNullOrWhiteSpace(color))
            {
                _color = System.Drawing.Color.FromArgb(RngInstance.Next(0, 256), RngInstance.Next(0, 256), RngInstance.Next(0, 256));
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
                            //rawColor = uint.Parse(color.ToColor(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        }
                        //return FergunResult.FromError(Locate("InvalidColor"));
                    }
                }
                _color = System.Drawing.Color.FromArgb(rawColor);
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
                    var msg = await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, $"{_color}.png");
                }
            }

            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [LongRunning]
        [Command("config", RunMode = RunMode.Async)]
        [Summary("configSummary")]
        [Alias("configuration", "settings")]
        public async Task<RuntimeResult> Config()
        {
            //Console.WriteLine($"Executing \"config\" in {Context.Guild.Name} for {Context.User}");

            var currentGuild = GetGuild() ?? new Guild(Context.Guild.Id);

            string listToShow = "";
            string[] configList = Locate("ConfigList").Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < configList.Length; i++)
            {
                listToShow += $"**{i + 1}.** {configList[i]}\n";
            }
            //bool hasReacted = false;
            IUserMessage message = null;

            string valueList =
                $"{Locate(currentGuild.CaptionbotAutoTranslate)}\n" +
                $"{Locate(currentGuild.AidAutoTranslate)}\n" +
                $"{Locate(currentGuild.TrackSelection)}";

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(Locate("FergunConfig"))
                .WithDescription(Locate("ConfigPrompt"))
                .AddField(Locate("Option"), listToShow, true)
                .AddField(Locate("Value"), valueList, true)
                .WithColor(FergunConfig.EmbedColor);

            async Task HandleReactionAsync(SocketReaction reaction)
            {
                FergunClient.Database.UpdateRecord("Guilds", currentGuild);
                valueList =
                    $"{Locate(currentGuild.CaptionbotAutoTranslate)}\n" +
                    $"{Locate(currentGuild.AidAutoTranslate)}\n" +
                    $"{Locate(currentGuild.TrackSelection)}";

                builder.Fields[1] = new EmbedFieldBuilder { Name = Locate("Value"), Value = valueList, IsInline = true };
                _ = message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                await message.ModifyAsync(x => x.Embed = builder.Build());
            }
            ReactionCallbackData data = new ReactionCallbackData(null, builder.Build(), false, false, TimeSpan.FromMinutes(2))
                .AddCallBack(new Emoji("1️⃣"), async (_, reaction) =>
                {
                    //hasReacted = true;
                    currentGuild.CaptionbotAutoTranslate = !currentGuild.CaptionbotAutoTranslate;
                    await HandleReactionAsync(reaction);
                })
                .AddCallBack(new Emoji("2️⃣"), async (_, reaction) =>
                {
                    currentGuild.AidAutoTranslate = !currentGuild.AidAutoTranslate;
                    await HandleReactionAsync(reaction);
                    //hasReacted = true;
                })
                .AddCallBack(new Emoji("3️⃣"), async (_, reaction) =>
                {
                    currentGuild.TrackSelection = !currentGuild.TrackSelection;
                    await HandleReactionAsync(reaction);
                    //hasReacted = true;
                })
                .AddCallBack(new Emoji("❌"), async (_, reaction) =>
                {
                    await message.TryDeleteAsync();
                });

            message = await InlineReactionReplyAsync(data);

            return FergunResult.FromSuccess();
        }

        [Command("editsnipe", RunMode = RunMode.Async)]
        [Summary("editsnipeSummary")]
        [Alias("esnipe")]
        public async Task Editsnipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            IMessage message = FergunClient.EditedMessages.FindLast(x => x.Channel.Id == channel.Id);

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
            builder.WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [Command("help")]
        [Summary("helpSummary")]
        [Alias("ayuda", "yardım")]
        public async Task<RuntimeResult> Help([Summary("helpParam1")] string commandName = null)
        {
            var builder = new EmbedBuilder();
            if (commandName == null)
            {
                var textCommands = _cmdService.Commands.Where(x => x.Module.Name == "Text").Select(x => x.Name);
                var utilityCommands = _cmdService.Commands.Where(x => x.Module.Name == "Utility").Select(x => x.Name);
                var moderationCommands = _cmdService.Commands.Where(x => x.Module.Name == "Moderation").Select(x => x.Name);
                var musicCommands = _cmdService.Commands.Where(x => x.Module.Name == "Music").Select(x => x.Name);
                var aidCommands = _cmdService.Commands.Where(x => x.Module.Group == "aid").Select(x => x.Name);
                var otherCommands = _cmdService.Commands.Where(x => x.Module.Name == "Other").Select(x => x.Name);
                var ownerCommands = _cmdService.Commands.Where(x => x.Module.Name == "Owner").Select(x => x.Name);
                var devCommandCount = _cmdService.Commands.Count(x => x.Module.Name == "Dev");
                int visibleCommandCount = _cmdService.Commands.Count() - devCommandCount;

                builder.WithTitle(Locate("CommandList"))
                    .AddField(Locate("TextCommands"), string.Join(", ", textCommands))
                    .AddField(Locate("UtilityCommands"), string.Join(", ", utilityCommands))
                    .AddField(Locate("ModerationCommands"), string.Join(", ", moderationCommands))
                    //.AddField(GetValue("EntertainmentCommands"), "trivia, ...")
                    .AddField(Locate("MusicCommands"), string.Join(", ", musicCommands))
                    .AddField(string.Format(Locate("AIDCommands"), GetPrefix()), string.Join(", ", aidCommands))
                    .AddField(Locate("OtherCommands"), string.Join(", ", otherCommands))
                    .AddField(Locate("OwnerCommands"), string.Join(", ", ownerCommands))
                    .AddField(Locate("Notes"), string.Format(Locate("NotesInfo"), GetPrefix()));

                string version = $"v{FergunClient.Version}";
                if (FergunClient.IsDebugMode)
                {
                    version += "-dev";
                }
                else
                {
                    builder.AddField("Links",
                        string.Format(Locate("Links"),
                        FergunClient.InviteLink,
                        FergunClient.DblBotPage,
                        $"{FergunClient.DblBotPage}/vote",
                        FergunClient.SupportServer));
                }
                builder.WithFooter(string.Format(Locate("HelpFooter"), version, visibleCommandCount))
                    .WithColor(FergunConfig.EmbedColor);

                await ReplyAsync(embed: builder.Build());
            }
            else
            {
                var command = _cmdService.Commands.FirstOrDefault(x => x.Aliases.Any(y => y == commandName.ToLowerInvariant()) && x.Module.Name != "Dev");
                if (command == null)
                {
                    return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
                }
                var embed = BuildCommandHelp(command);
                await ReplyAsync(embed: embed);
            }
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("identify", RunMode = RunMode.Async)]
        [Summary("identifySummary")]
        [Alias("captionbot")]
        [Remarks("NoUrlPassed")]
        public async Task<RuntimeResult> Identify([Summary("identifyParam1")] string url = null)
        {
            string errorReason;
            (url, errorReason) = await GetLastUrl(50, true, url);
            if (url == null)
            {
                return FergunResult.FromError(Locate(errorReason));
            }

            var data = new Dictionary<string, string>
            {
                { "Content", url },
                { "Type", "CaptionRequest" }
            };

            string text;
            using (var content = new FormUrlEncodedContent(data))
            {
                var response = await _httpClient.PostAsync(new Uri("https://captionbot.azurewebsites.net/api/messages?language=en-US"), content);
                text = await response.Content.ReadAsStringAsync();
            }
            text = text.Trim('\"');

            bool autoTranslate = GetGuild()?.CaptionbotAutoTranslate ?? FergunConfig.CaptionbotAutoTranslateDefault;
            if (autoTranslate && GetLanguage() != "en")
            {
                var result = await TranslateSimpleAsync(text, GetLanguage(), "en");
                if (result.Error == null)
                {
                    text = result.Text;
                }
            }

            await ReplyAsync(text);
            return FergunResult.FromSuccess();
        }

        // BUGS: Some pages may not have images, idk why that happens, but at least the command doesn't throw any errors.
        [LongRunning]
        [Command("img", RunMode = RunMode.Async), Ratelimit(1, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("imgSummary")]
        [Alias("im", "image", "ddgi")]
        public async Task<RuntimeResult> Img([Remainder, Summary("imgParam1")] string query)
        {
            query = query.Trim();
            Console.WriteLine($"Executing \"img\" in {(Context.IsPrivate ? $"@{Context.User}" : $"{Context.Guild.Name}/{Context.Channel.Name}")} for {Context.User} with query: \"{query}\"");

            // Considering a DM channel a SFW channel.
            bool isNsfwChannel = !Context.IsPrivate && (Context.Channel as ITextChannel).IsNsfw;

            var pages = new List<PaginatedMessage.Page>();

            var cached = ImgCache.Find(x => x.Query == query && x.IsNsfw == isNsfwChannel);
            if (cached == null)
            {
                DdgResponse search;
                try
                {
                    search = await DdgApi.SearchImagesAsync(query, !isNsfwChannel ? SafeSearch.Strict : SafeSearch.Off);
                }
                catch (HttpRequestException)
                {
                    return FergunResult.FromError(Locate("AnErrorOcurred"));
                }
                catch (Exception e)
                {
                    return FergunResult.FromError(e.Message);
                }
                if (search.Results.Count == 0)
                {
                    return FergunResult.FromError(Locate("NoResultsFound"));
                }

                foreach (var item in search.Results)
                {
                    pages.Add(new PaginatedMessage.Page()
                    {
                        Title = item.Title.Truncate(EmbedBuilder.MaxTitleLength),
                        ImageUrl = Uri.EscapeUriString(Uri.UnescapeDataString(item.Image)),
                        Url = item.Url
                    });
                }
                ImgCache.Add(new CachedPages(query, pages, isNsfwChannel));
            }
            else
            {
                pages = cached.Pages;
            }
            var pager = new PaginatedMessage()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.ToString(),
                    IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()
                },
                Description = Locate("ImageSearch"),
                Pages = pages,
                Color = new Discord.Color(FergunConfig.EmbedColor),
                Options = new PaginatedAppearanceOptions()
                {
                    InformationText = Locate("PaginatorHelp"),
                    FooterFormat = Locate("PaginatorFooter"),
                    Timeout = TimeSpan.FromMinutes(10),
                    ActionOnTimeout = ActionOnTimeout.DeleteReactions
                }
            };

            await PagedReplyAsync(pager, new ReactionList(true, true, true, true, true, true, false));

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [Command("invert", RunMode = RunMode.Async), Ratelimit(2, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("invertSummary")]
        [Alias("negate", "negative")]
        public async Task<RuntimeResult> Invert([Remainder, Summary("invertParam1")] string url = null)
        {
            string errorReason;
            (url, errorReason) = await GetLastUrl(50, true, url);
            if (url == null)
            {
                return FergunResult.FromError(Locate(errorReason));
            }

            using (Stream response = await _httpClient.GetStreamAsync(url))
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
                if (invertedFile.Length > 8000000)
                {
                    return FergunResult.FromError("The file is too large.");
                }
                invertedFile.Position = 0;
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, invertedFile, $"invert{format.FileExtensionFromEncoder()}");
            }

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("ocr", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrSummary")]
        [Remarks("NoUrlPassed")]
        public async Task<RuntimeResult> Ocr([Summary("ocrParam1")] string url = null)
        {
            string errorReason;
            (url, errorReason) = await GetLastUrl(50, true, url);
            if (url == null)
            {
                return FergunResult.FromError(Locate(errorReason));
            }

            var result = await OcrSimpleAsync(url);
            if (!int.TryParse(result.Item1, out int processTime))
            {
                return FergunResult.FromError(Locate(result.Item1));
            }
            string text = result.Item2;

            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 6)
            {
                var response = await Hastebin.UploadAsync(text);
                text = Format.Url(Locate("HastebinLink"), response.GetLink());
            }
            else
            {
                text = $"```{text}```";
            }

            var builder = new EmbedBuilder()
                    .WithTitle(Locate("OcrResults"))
                    .AddField(Locate("Output"), text)
                    .WithFooter(string.Format(Locate("ProcessingTime"), processTime))
                    .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        // BUGS: If more than 1 image is processed at the same time, the ocr will throw InvalidOperationException:
        // Only one image can be processed at once (Please make sure you dispose of the page once your finished with it.)
        //[LongRunning]
        //[Command("ocr2", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        //[Summary("ocr2Summary")]
        //[Remarks("NoUrlPassed")]
        //public async Task<RuntimeResult> Ocr2([Summary("ocr2Param1")] string url = null)
        //{
        //    string errorReason;
        //    (url, errorReason) = await GetLastUrl(50, true, url, 500000);
        //    if (url == null)
        //    {
        //        return FergunResult.FromError(Locate(errorReason));
        //    }

        //    var sw = new Stopwatch();
        //    string text;

        //    using (Stream response = await _httpClient.GetStreamAsync(url))
        //    {
        //        // Starting here because i only want to measure the time that takes to convert the stream to bitmap, resize, convert to pix and process the image
        //        sw.Start();
        //        Bitmap img = new Bitmap(response);

        //        if (img.Width < 800 && img.Height < 800)
        //        {
        //            try
        //            {
        //                img = img.Resize(img.Width * 2, img.Height * 2);
        //            }
        //            catch (ArgumentException) { } // Ignore the resize error
        //        }
        //        using (Pix pix = PixConverter.ToPix(img))
        //        using (var page = TessEngine.Process(pix, PageSegMode.SparseText))
        //        {
        //            text = page.GetText();
        //        }
        //        img.Dispose();
        //        sw.Stop();
        //    }
        //    if (string.IsNullOrWhiteSpace(text))
        //    {
        //        return FergunResult.FromError(Locate("OcrEmpty"));
        //    }

        //    text = Regex.Replace(text, @"^\s*$\n|\r", string.Empty, RegexOptions.Multiline)
        //        .Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase)
        //        .Trim();

        //    if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 6)
        //    {
        //        var response = await Hastebin.UploadAsync(text);
        //        text = Format.Url(Locate("HastebinLink"), response.GetLink());
        //    }
        //    else
        //    {
        //        text = $"```{text}```";
        //    }

        //    var builder = new EmbedBuilder()
        //        .WithTitle(Locate("OcrResults"))
        //        .AddField(Locate("Output"), text)
        //        .WithFooter(string.Format(Locate("ProcessingTime"), sw.ElapsedMilliseconds))
        //        .WithColor(FergunConfig.EmbedColor);

        //    await ReplyAsync(embed: builder.Build());
        //    return FergunResult.FromSuccess();
        //}

        [LongRunning]
        [Command("ocrtranslate", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrtranslateSummary")]
        [Alias("ocrtr")]
        [Remarks("NoUrlPassed")]
        public async Task<RuntimeResult> Ocrtr([Summary("ocrtranslateParam1")] string target,
            [Summary("ocrtranslateParam2")] string url = null)
        {
            if (!GoogleTranslator.IsLanguageSupported(new Language("", target)))
            {
                return FergunResult.FromError($"{Locate("InvalidLanguage")}\n{string.Join(" ", Translators.SupportedLanguages.Select(x => Format.Code(x)))}");
            }

            string errorReason;
            (url, errorReason) = await GetLastUrl(50, true, url);
            if (url == null)
            {
                return FergunResult.FromError(Locate(errorReason));
            }

            var ocrResult = await OcrSimpleAsync(url);
            if (!int.TryParse(ocrResult.Item1, out int processTime))
            {
                return FergunResult.FromError(Locate(ocrResult.Item1));
            }
            string text = ocrResult.Item2;

            var sw = Stopwatch.StartNew();
            var result = await TranslateSimpleAsync(text, target);
            sw.Stop();
            if (result.Error != null)
            {
                return FergunResult.FromError(Locate(result.Error));
            }

            if (result.Text.Length > EmbedFieldBuilder.MaxFieldValueLength - 6)
            {
                var response = await Hastebin.UploadAsync(result.Text);
                result.Text = Format.Url(Locate("HastebinLink"), response.GetLink());
            }
            else
            {
                result.Text = $"```{result.Text}```";
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("OcrtrResults"))
                .AddField(Locate("Input"), $"```{text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 6)}```")
                .AddField(Locate("SourceLanguage"), result.Source?.FullName ?? "???", false)
                .AddField(Locate("TargetLanguage"), result.Target.FullName, false)
                .AddField(Locate("Result"), result.Text, false)
                .WithFooter(string.Format(Locate("ProcessingTime"), processTime + sw.ElapsedMilliseconds))
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("ping")]
        [Summary("pingSummary")]
        public async Task Ping()
        {
            var sw = Stopwatch.StartNew();
            await SendEmbedAsync("**Pong!**");
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            FergunClient.Database.Find<Guild>("Guilds", _ => true);
            sw2.Stop();

            await SendEmbedAsync($"⏱**Message**: {sw.ElapsedMilliseconds}ms\n\n{MongoDbEmote}**Database**: {Math.Round(sw2.Elapsed.TotalMilliseconds, 2)}ms");
            //await msg.ModifyAsync(x => x.Content = $"**Pong!** {sw.ElapsedMilliseconds}ms\nWebsocket: {Context.Client.Latency}ms");
        }

        [LongRunning]
        [Command("resize", RunMode = RunMode.Async), Ratelimit(1, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("resizeSummary")]
        [Alias("waifu2x", "w2x")]
        [Remarks("NoUrlPassed")]
        public async Task<RuntimeResult> Resize([Summary("resizeParam1")] string url = null)
        {
            string errorReason;
            (url, errorReason) = await GetLastUrl(50, true, url);
            if (url == null)
            {
                return FergunResult.FromError(Locate(errorReason));
            }

            var data = new Dictionary<string, string>
            {
                { "image", url }
            };

            var content = new FormUrlEncodedContent(data);
            string responseString;

            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepai.org/api/waifu2x"))
            {
                request.Headers.Add("Api-Key", FergunConfig.DeepAiApiKey);
                request.Content = content;
                var response = await _deepAiClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }

            JToken token = JObject.Parse(responseString);
            string resultUrl = token.Value<string>("output_url");
            if (string.IsNullOrWhiteSpace(resultUrl))
            {
                return FergunResult.FromError(Locate("AnErrorOcurred"));
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ResizeResults"))
                .WithImageUrl(resultUrl)
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("roleinfo")]
        [Summary("roleinfoSummary")]
        [Alias("role")]
        public async Task<RuntimeResult> Roleinfo([Remainder, Summary("roleinfoParam1")] SocketRole role)
        {
            var builder = new EmbedBuilder()
                .WithTitle(Locate("RoleInfo"))

                .AddField(Locate("Name"), role.IsEveryone ? Format.Code(role.Name) : role.Name, true)
                .AddField(Locate("Color"), $"{role.Color} ({role.Color.R}, {role.Color.G}, {role.Color.B})", true)
                .AddField(Locate("IsMentionable"), Locate(role.IsMentionable), true)

                .AddField("ID", role.Id, true)
                .AddField(Locate("IsHoisted"), Locate(role.IsHoisted), true)
                .AddField(Locate("Position"), role.Position, true)

                .AddField(Locate("Permissions"), role.Permissions.RawValue == 0 ? Locate("None") : Format.Code(string.Join("`, `", role.Permissions.ToList())), false)

                .AddField(Locate("MemberCount"), role.Members.Count(), true)
                .AddField(Locate("CreatedAt"), role.CreatedAt, true)
                .AddField(Locate("Mention"), role.IsEveryone ? Format.Code(role.Mention) : role.Mention, true)

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
        public async Task<RuntimeResult> Screenshot([Summary("screenshotParam1")] string url)
        {
            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                return FergunResult.FromError(Locate("InvalidUrl"));
            }

            ApiFlashResponse response;
            try
            {
                response = await ApiFlash.UrlToImageAsync(FergunConfig.ApiFlashAccessKey, Uri.EscapeDataString(uri.AbsoluteUri), ApiFlash.FormatType.png, "400,403,404,500-511");
            }
            catch (ArgumentException)
            {
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            catch (WebException e)
            {
                return FergunResult.FromError(e.Message);
            }

            if (response.ErrorMessage != null)
            {
                return FergunResult.FromError(response.ErrorMessage);
            }

            //var data = new Dictionary<string, string>
            //{
            //    { "image", Response.Url }
            //};

            //string responseString;

            //using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepai.org/api/nsfw-detector"))
            //{
            //    request.Headers.Add("Api-Key", Config.DeepAiApiKey);
            //    request.Content = new FormUrlEncodedContent(data);
            //    var response = await DeepAIClient.SendAsync(request);
            //    responseString = await response.Content.ReadAsStringAsync();
            //}

            //JToken token2 = JObject.Parse(responseString);
            //double score = (double)token2["output"]["nsfw_score"];
            //if (score > 0.3 && !(Context.Channel as SocketTextChannel).IsNsfw)
            //{
            //    await ReplyAsync(GetValue("ScreenshotNSFW"));
            //    return;
            //}

            using (Stream image = await _httpClient.GetStreamAsync(new Uri(response.Url)))
            {
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, image, "screenshot.png");
            }
            return FergunResult.FromSuccess();
        }

        [Command("serverinfo", RunMode = RunMode.Async)]
        [Summary("serverinfoSummary")]
        [Alias("server", "guild", "guildinfo")]
        public async Task<RuntimeResult> Serverinfo(ulong? serverId = null)
        {
            SocketGuild server;
            if (Context.User.Id == (await Context.Client.GetApplicationInfoAsync()).Owner.Id)
            {
                server = serverId == null ? Context.Guild : Context.Client.GetGuild(serverId.Value);
                if (server == null)
                {
                    return FergunResult.FromError(Locate("GuildNotFound"));
                }
            }
            else
            {
                server = Context.Guild;
            }

            //IReadOnlyCollection<IGuildUser> users;

            //if (server.MemberCount > 200)
            //{
            //    users = await (server as IGuild).GetUsersAsync();
            //}
            //else
            //{
            //    if (!server.HasAllMembers)
            //    {
            //        await server.DownloadUsersAsync();
            //    }
            //    users = server.Users;
            //}
            var users = Context.Guild.Users;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ServerInfo"))

                .AddField(Locate("Name"), server.Name, true)
                .AddField(Locate("Owner"), MentionUtils.MentionUser(server.OwnerId), true)
                .AddField("ID", server.Id, true)

                .AddField(Locate("CategoryCount"), server.CategoryChannels.Count, true) // Members
                .AddField(Locate("ChannelCount"), $"{server.TextChannels.Count + server.VoiceChannels.Count} ({TextEmote} {server.TextChannels.Count} **|** {VoiceEmote} {server.VoiceChannels.Count})", true)
                .AddField(Locate("RoleCount"), server.Roles.Count, true)

                .AddField(Locate("DefaultChannel"), server.DefaultChannel.Mention, true)
                .AddField(Locate("Region"), Format.Code(server.VoiceRegionId), true)
                .AddField(Locate("VerificationLevel"), Locate(server.VerificationLevel.ToString()), true)

                .AddField(Locate("BoostTier"), (int)server.PremiumTier, true)
                .AddField(Locate("BoostCount"), server.PremiumSubscriptionCount, true)
                .AddField(Locate("ServerFeatures"), server.Features.Count == 0 ? Locate("None") : string.Join(", ", server.Features), true)

                .AddField(Locate("Members"), $"{server.MemberCount} (Bots: {users.Count(x => x.IsBot)}) **|** " +
                $"{OnlineEmote} {users.Count(x => x.Status == UserStatus.Online)} **|** " +
                $"{IdleEmote} {users.Count(x => x.Status == UserStatus.Idle)} **|** " +
                $"{DndEmote} {users.Count(x => x.Status == UserStatus.DoNotDisturb)} **|** " +
                $"{StreamingEmote} {users.Count(x => x.Activity != null && x.Activity.Type == ActivityType.Streaming)} **|** " +
                $"{OfflineEmote} {users.Count(x => x.Status == UserStatus.Offline)}");

            /*
            .AddField(Locate("Members"), $"{server.MemberCount} (Bots: {server.Users.Count(x => x.IsBot)}) **|** " +
            $"{OnlineEmote} {server.Users.Count(x => x.Status == UserStatus.Online)} **|** " +
            $"{IdleEmote} {server.Users.Count(x => x.Status == UserStatus.Idle)} **|** " +
            $"{DndEmote} {server.Users.Count(x => x.Status == UserStatus.DoNotDisturb)} **|** " +
            $"{StreamingEmote} {server.Users.Count(x => x.Activity != null && x.Activity.Type == ActivityType.Streaming)} **|** " +
            $"{OfflineEmote} {server.Users.Count(x => x.Status == UserStatus.Offline)}");
            */
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
            // Maybe there's a better way to get the animated icon..?
            builder.WithThumbnailUrl(server.Features.Any(x => x == "ANIMATED_ICON") ? server.IconUrl.Substring(0, server.IconUrl.Length - 3) + "gif" : server.IconUrl);
            //if (server.Features.Any(x => x == "BANNER"))
            //{
            //}
            builder.WithColor(FergunConfig.EmbedColor);
            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("snipe", RunMode = RunMode.Async)]
        [Summary("snipeSummary")]
        public async Task Snipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            IMessage message = FergunClient.DeletedMessages.FindLast(x => x.Channel.Id == channel.Id);

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
            builder.WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [LongRunning]
        [Command("translate", RunMode = RunMode.Async)]
        [Summary("translateSummary")]
        [Alias("tr")]
        public async Task<RuntimeResult> Translate([Summary("translateParam1")] string target,
            [Remainder, Summary("translateParam2")] string text)
        {
            if (!GoogleTranslator.IsLanguageSupported(new Language("", target)))
            {
                return FergunResult.FromError($"{Locate("InvalidLanguage")}\n{string.Join(" ", Translators.SupportedLanguages.Select(x => Format.Code(x)))}");
            }

            var result = await TranslateSimpleAsync(text, target);
            if (result.Error != null)
            {
                return FergunResult.FromError(Locate(result.Error));
            }

            if (result.Text.Length > EmbedFieldBuilder.MaxFieldValueLength - 6)
            {
                var response = await Hastebin.UploadAsync(result.Text);
                result.Text = Format.Url(Locate("HastebinLink"), response.GetLink());
            }
            else
            {
                result.Text = $"```{result.Text}```";
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("TranslationResults"))
                .AddField(Locate("SourceLanguage"), result.Source?.FullName ?? "???", false)
                .AddField(Locate("TargetLanguage"), result.Target.FullName, false)
                .AddField(Locate("Result"), result.Text, false)
                .WithThumbnailUrl("https://fergun.is-inside.me/u7fSdkx8.png")
                .WithColor(FergunConfig.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("tts", RunMode = RunMode.Async), Ratelimit(2, FergunClient.GlobalCooldown, Measure.Minutes)]
        [Summary("Text to speech.")]
        [Alias("texttospeech", "t2s")]
        public async Task<RuntimeResult> Tts([Summary("ttsParam1")] string target,
            [Remainder, Summary("ttsParam2")] string text)
        {
            target = target.ToLowerInvariant();
            text = text.ToLowerInvariant();

            if (!GoogleTTS.IsLanguageSupported(new Language("", target)))
            {
                //return CustomResult.FromError(GetValue("InvalidLanguage"));
                text = $"{target} {text}";
                target = "en";
            }
            byte[] bytes = null;
            var cached = TtsCache.Find(x => x.Language == target && x.Text == text);
            if (cached == null)
            {
                try
                {
                    bytes = await GoogleTTS.GetTtsAsync(text, target);
                }
                catch (HttpRequestException)
                {
                    return FergunResult.FromError(Locate("AnErrorOcurred"));
                }
                cached = new CachedTts(target, text, bytes);
                TtsCache.Add(cached);
            }
            using (var stream = new MemoryStream(cached.Tts))
            {
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, "tts.mp3");
            }
            return FergunResult.FromSuccess();
        }

        // The attribute order matters
        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        [LongRunning]
        [Command("urban", RunMode = RunMode.Async)]
        [Summary("urbanSummary")]
        [Alias("ud")]
        public async Task<RuntimeResult> Urban([Remainder, Summary("urbanParam1")] string query = null)
        {
            Console.WriteLine($"Executing \"urban\" in {(Context.IsPrivate ? $"@{Context.User}" : $"{Context.Guild.Name}/{Context.Channel.Name}")} for {Context.User} with query: \"{query}\"");
            UrbanApi urban = new UrbanApi();
            UrbanResponse search = null;
            CachedPages cached = null;

            query = query?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                search = urban.GetRandomWords();
            }
            else
            {
                cached = UrbanCache.Find(x => x.Query == query);
                if (cached == null)
                {
                    search = urban.SearchWord(query);
                    if (search.Definitions.Count == 0)
                    {
                        return FergunResult.FromError(Locate("NoResults"));
                    }
                }
            }

            var pages = new List<PaginatedMessage.Page>();
            if (cached == null)
            {
                foreach (var item in search.Definitions)
                {
                    // Nice way to replace all ocurrences to a custom string.
                    item.Definition = _bracketReplacer.Replace(item.Definition,
                                                              m => Format.Url(m.Groups[1].Value, "https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)"));
                    if (!string.IsNullOrEmpty(item.Example))
                    {
                        item.Example = _bracketReplacer.Replace(item.Example,
                                                              m => Format.Url(m.Groups[1].Value, "https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)"));
                    }
                    List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>
                    {
                        new EmbedFieldBuilder()
                        {
                            Name = Locate("Example"),
                            Value = string.IsNullOrEmpty(item.Example) ? Locate("NoExample") : item.Example.Truncate(EmbedFieldBuilder.MaxFieldValueLength),
                            IsInline = false
                        },
                        new EmbedFieldBuilder()
                        {
                            Name = "👍",
                            Value = item.ThumbsUp,
                            IsInline = true
                        },
                        new EmbedFieldBuilder()
                        {
                            Name = "👎",
                            Value = item.ThumbsDown,
                            IsInline = true
                        }
                    };

                    var author = new EmbedAuthorBuilder()
                        .WithName($"{Locate("By")} {item.Author}");

                    pages.Add(new PaginatedMessage.Page()
                    {
                        Author = author,
                        Title = item.Word.Truncate(EmbedBuilder.MaxTitleLength),
                        Description = item.Definition.Truncate(EmbedBuilder.MaxDescriptionLength),
                        Url = item.Permalink,
                        Fields = fields,
                        TimeStamp = item.WrittenOn
                    });
                }
                UrbanCache.Add(new CachedPages(query, pages, true));
            }
            else
            {
                pages = cached.Pages;
            }

            var pager = new PaginatedMessage()
            {
                //Title = result.Item2.First(),
                Pages = pages,
                Color = new Discord.Color(FergunConfig.EmbedColor),
                Options = new PaginatedAppearanceOptions()
                {
                    FooterFormat = $"Urban Dictionary {(string.IsNullOrWhiteSpace(query) ? "(Random words)" : "")} - {Locate("PaginatorFooter")}",
                    Timeout = TimeSpan.FromMinutes(10),
                    ActionOnTimeout = ActionOnTimeout.DeleteReactions
                }
            };

            var reactions = new ReactionList()
            {
                First = true,
                Backward = true,
                Forward = true,
                Last = true,
                Stop = true,
                Jump = true,
                Info = false
            };

            await PagedReplyAsync(pager, reactions);

            return FergunResult.FromSuccess();
        }

        [Command("userinfo")]
        [Summary("userinfoSummary")]
        [Alias("ui", "user", "whois")]
        public async Task<RuntimeResult> Userinfo([Remainder, Summary("userinfoParam1")] IUser user = null)
        {
            user ??= Context.User;

            string avatarUrl = user.GetAvatarUrl(Discord.ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();

            System.Drawing.Color avatarColor;
            string thumbnail = user.GetAvatarUrl(Discord.ImageFormat.Png, 128) ?? user.GetDefaultAvatarUrl();
            using (Stream response = await _httpClient.GetStreamAsync(new Uri(thumbnail)))
            {
                using (Bitmap img = new Bitmap(response))
                {
                    avatarColor = img.GetAverageColor();
                }
            }

            string activity;
            if (user.Activity == null)
            {
                activity = Locate("None");
            }
            else if (user.Activity.Type == ActivityType.CustomStatus)
            {
                activity = $"**{(user.Activity as CustomStatusGame).State}**";
            }
            else
            {
                activity = $"{user.Activity.Type} **{user.Activity.Name}**";
            }
            var guildUser = user as IGuildUser;

            List<string> clients = new List<string>();
            if (user.ActiveClients.Count > 0)
            {
                clients = user.ActiveClients.Select(x =>
                x == ClientType.Desktop ? "🖥" :
                x == ClientType.Mobile ? "📱" :
                x == ClientType.Web ? "🌐" : "").ToList();
            }
            var builder = new EmbedBuilder()
                .WithTitle(Locate("UserInfo"))
                .AddField(Locate("Name"), user.ToString(), false)
                .AddField("Nickname", guildUser?.Nickname ?? Locate("None"), false)
                .AddField("ID", user.Id, false)
                .AddField(Locate("Activity"), activity, true)
                .AddField(Locate("ActiveClients"), user.ActiveClients.Count == 0 ? "?" : string.Join(" ", clients), true)
                .AddField(Locate("IsBot"), Locate(user.IsBot), false)
                .AddField(Locate("CreatedAt"), user.CreatedAt)
                .AddField(Locate("GuildJoinDate"), guildUser?.JoinedAt?.ToString() ?? "N/A")
                .AddField(Locate("BoostingSince"), guildUser?.PremiumSince?.ToString() ?? "N/A")
                //.AddField(GetValue("Roles"), !(user is IGuildUser) || guildUser.RoleIds.Count == 1 ? GetValue("None") : string.Join(", ", guildUser.RoleIds.Skip(1).Select(x => Context.Guild.GetRole(x).Mention)))
                .WithThumbnailUrl(avatarUrl)
                .WithColor(new Discord.Color(avatarColor.R, avatarColor.G, avatarColor.B));

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("wikipedia", RunMode = RunMode.Async)]
        [Summary("wikipediaSummary")]
        [Alias("wiki")]
        public async Task<RuntimeResult> Wikipedia([Remainder, Summary("wikipediaParam1")] string query)
        {
            string response;
            using (WebClient wc = new WebClient())
            {
                // I would want to know who did the awful json response structure.
                response = await wc.DownloadStringTaskAsync($"https://{GetLanguage()}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&format=json");
            }
            //response = '[' + SearchResponse.Substring(SearchResponse.IndexOf(',') + 1, SearchResponse.Length - SearchResponse.IndexOf(',') - 1);
            List<dynamic> search = search = JsonConvert.DeserializeObject<List<dynamic>>(response);
            string langToUse = GetLanguage();
            if (search[1].Count == 0)
            {
                if (langToUse == "en")
                {
                    return FergunResult.FromError(Locate("NoResults"));
                }
                else
                {
                    langToUse = "en";
                    using (WebClient wc = new WebClient())
                    {
                        response = await wc.DownloadStringTaskAsync($"https://{langToUse}.wikipedia.org/w/api.php?action=opensearch&search={query}&format=json");
                    }
                    search = JsonConvert.DeserializeObject<List<dynamic>>(response);
                    if (search[1].Count == 0)
                    {
                        return FergunResult.FromError(Locate("NoResults"));
                    }
                }
            }
            using (WebClient wc = new WebClient())
            {
                string articleUrl = search[search.Count - 1][0];
                response = await wc.DownloadStringTaskAsync($"https://{langToUse}.wikipedia.org/api/rest_v1/page/summary/{articleUrl.Substring(30)}");
            }
            var article = JsonConvert.DeserializeObject<WikiArticle>(response);
            //using (WebClient wc = new WebClient())
            //{
            //    SearchResponse = await wc.DownloadStringTaskAsync($"https://{(IsMobile() ? "m." : "")}{GetLanguage()}.wikipedia.org/w/api.php?action=query&origin=*&generator=search&prop=info|extracts|pageimages&piprop=original&pilicense=any&gsrsearch={query}&gsrlimit=1&exintro=1&explaintext=1&exchars=1024&exlimit=20&utf8=&format=json&formatversion=2");
            //}
            //WikipediaResult Search = WikipediaResult.FromJson(SearchResponse);

            //if (Search.Query == null)
            //{
            //    return FergunResult.FromError(GetValue("NoResults"));
            //}

            //Search.Query.Pages = Search.Query.Pages.OrderBy(x => x.Index).ToArray();

            //var builder = new EmbedBuilder()
            //    .WithAuthor(Context.User)
            //    .WithTitle(Search.Query.Pages[0].Title)
            //    //.WithThumbnailUrl(Search.Originalimage.Source)
            //    .WithDescription(Search.Query.Pages[0].Extract)
            //    .WithFooter(GetValue("WikipediaSearch"))
            //    .WithColor(FergunConfig.EmbedColor);

            var builder = new EmbedBuilder()
                .WithAuthor(Context.User)
                .WithTitle(article.Title.Truncate(EmbedBuilder.MaxTitleLength))
                //.WithThumbnailUrl(Article.Originalimage.Source)
                .WithDescription(article.Extract.Truncate(EmbedFieldBuilder.MaxFieldValueLength))
                .WithFooter(Locate("WikipediaSearch"))
                .WithThumbnailUrl("https://upload.wikimedia.org/wikipedia/commons/thumb/8/80/Wikipedia-logo-v2.svg/500px-Wikipedia-logo-v2.svg.png")
                .WithColor(FergunConfig.EmbedColor);

            //string url = $"https://www.{GetLanguage()}.wikipedia.org/wiki/{HttpUtility.UrlPathEncode(Search.Query.Pages[0].Title)}";
            //string url = $"https://{GetLanguage()}.wikipedia.org/wiki/{Search.Query.Pages[0].Title.Replace(' ', '_').Replace("\"", "%22")}";

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
            }

            if (!Context.IsPrivate && (Context.Channel as ITextChannel).IsNsfw)
            {
                string decodedUrl;
                // Microsoft bug..
                if (article.Originalimage?.Source != null && Uri.IsWellFormedUriString(decodedUrl = Uri.UnescapeDataString(article.Originalimage.Source), UriKind.Absolute))
                {
                    builder.ThumbnailUrl = decodedUrl;
                }
            }

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("xkcd")]
        [Summary("xkcdSummary")]
        public async Task<RuntimeResult> Xkcd([Summary("xkcdParam1")] int? number = null)
        {
            UpdateLastComic();
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

            XkcdResponse xkcd = JsonConvert.DeserializeObject<XkcdResponse>(response);

            var builder = new EmbedBuilder()
                .WithTitle(xkcd.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl($"https://xkcd.com/{xkcd.Num}/")
                .WithImageUrl(xkcd.Img)
                .WithFooter(xkcd.Alt.Truncate(EmbedFooterBuilder.MaxFooterTextLength))
                .WithTimestamp(new DateTime(int.Parse(xkcd.Year), int.Parse(xkcd.Month), int.Parse(xkcd.Day)));

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        //[LongRunning]
        [Command("ytrandom", RunMode = RunMode.Async)]
        [Summary("ytrandomSummary")]
        [Alias("ytrand")]
        public async Task<RuntimeResult> Ytrandom()
        {
            if (VideoCache.Count == 0)
            {
                if (_isCreatingCache)
                {
                    return FergunResult.FromError(Locate("CreatingVideoCache"));
                }
                _isCreatingCache = true;

                await ReplyAsync(Locate("EmptyCache"));
                await Context.Channel.TriggerTypingAsync();
                Console.WriteLine($"Creating video cache in {(Context.IsPrivate ? $"{Context.Channel}" : $"{Context.Guild.Name}/{Context.Channel.Name}")} for {Context.User}");

                List<Task> tasks = CreateVideoTasks();
                
                await Task.WhenAll(tasks);
                _isCreatingCache = false;
            }
            string url;
            lock (_videoCacheLock)
            {
                int randomInt = RngInstance.Next(VideoCache.Count);
                url = $"https://www.youtube.com/watch?v={VideoCache[randomInt]}";
                VideoCache.RemoveAt(randomInt);
            }
            await ReplyAsync(url);
            return FergunResult.FromSuccess();
        }

        // Helper methods

        private static List<Task> CreateVideoTasks()
        {
            List<Task> tasks = new List<Task>();
            while (tasks.Count < FergunConfig.VideoCacheSize)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        string randStr = RandomString(RngInstance.Next(5, 7));
                        //var items = await YTClient.SearchVideosAsync(rand, 1);
                        var items = await _ytClient.Search.GetVideosAsync(randStr).BufferAsync(5);
                        if (items.Count != 0)
                        {
                            lock (_videoCacheLock)
                            {
                                string randomId = items[RngInstance.Next(items.Count)].Id;
                                VideoCache.Add(randomId);
                                Console.WriteLine($"Added 1 item to Video cache (random string: {randStr}, search count: {items.Count}, selected id: {randomId}), total count: {VideoCache.Count}");
                            }
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"No videos found on {randStr}");
                        }
                    }
                }));
            }
            return tasks;
        }

        private static async Task<long?> GetUrlContentLengthAsync(string url)
        {
            var response = await _httpClient.GetAsync(new UriBuilder(url).Uri, HttpCompletionOption.ResponseHeadersRead);
            return response.Content.Headers.ContentLength;
        }

        private static async Task<string> GetUrlMediaTypeAsync(string url)
        {
            var response = await _httpClient.GetAsync(new UriBuilder(url).Uri, HttpCompletionOption.ResponseHeadersRead);
            return response.Content.Headers.ContentType.MediaType.ToLowerInvariant();
        }

        private static async Task<bool> IsImageUrlAsync(string url)
        {
            //Uri uri;
            //try
            //{
            //    uri = new UriBuilder(url).Uri;
            //}
            //catch
            //{
            //    return false;
            //}
            string mediaType;
            try
            {
                mediaType = await GetUrlMediaTypeAsync(url);
            }
            catch (UriFormatException)
            {
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }

            return mediaType.ToLowerInvariant().StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<(string, string)> OcrSimpleAsync(string url)
        {
            if (!Enum.TryParse((await GetUrlMediaTypeAsync(url)).Substring(6), true, out OCRSpace.FileType fileType))
            {
                return ("InvalidFileType", null);
            }

            OCRSpace.OCRSpaceResponse ocr;
            try
            {
                ocr = await OCRSpace.PerformOcrFromUrlAsync(FergunConfig.OCRSpaceApiKey, url, fileType: fileType, ocrEngine: OCRSpace.OCREngine.Engine1);
            }
            catch (WebException)
            {
                return ("OcrApiError", null);
            }

            if (ocr.IsErroredOnProcessing || ocr.OcrExitCode != 1)
            {
                return (ocr.ErrorMessage[0], null);
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

        private static async Task<SimpleTranslationResult> TranslateSimpleAsync(string text, Language target, Language source)
            => await TranslateSimpleAsync(text, target.ISO639, source == Language.Auto ? "" : source.ISO639);

        private static async Task<SimpleTranslationResult> TranslateSimpleAsync(string text, string target, string source = "")
        {
            string resultError = null;
            string resultTranslation = null;
            Language resultTarget = GoogleTranslator.GetLanguageByISO(target);
            Language resultSource = null;

            bool useBing = false;
            text = text.Replace("`", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            try
            {
                var translator = new GoogleTranslator();
                var result = await translator.TranslateLiteAsync(text, string.IsNullOrEmpty(source) ? Language.Auto : new Language("", source), resultTarget);

                resultTranslation = result.MergedTranslation;
                resultSource = result.LanguageDetections[0].Language;
            }
            catch (GoogleTranslateIPBannedException)
            {
                useBing = true;
            }
            catch (HttpRequestException)
            {
                useBing = true;
            }
            if (useBing)
            {
                try
                {
                    var result = await Translators.TranslateBingAsync(text, target);

                    resultTranslation = result[0].Translations[0].Text;
                    resultSource = GoogleTranslator.GetLanguageByISO(result[0].DetectedLanguage.Language);
                }
                catch (JsonSerializationException)
                {
                    resultError = "ErrorInTranslation";
                }
                catch (HttpRequestException)
                {
                    resultError = "ErrorInTranslation";
                }
                catch (ArgumentException)
                {
                    resultError = "LanguageNotFound";
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
            if (_timeToCheckComic != null && DateTime.UtcNow < _timeToCheckComic)
            {
                return;
            }
            string response;
            using (WebClient wc = new WebClient())
            {
                response = wc.DownloadString("https://xkcd.com/info.0.json");
            }
            _lastComic = JsonConvert.DeserializeObject<XkcdResponse>(response);
            _timeToCheckComic = DateTime.UtcNow.AddDays(1);
        }

        private Embed BuildCommandHelp(CommandInfo command)
        {
            //Maybe there's a better way to make a dynamic help?
            var builder = new EmbedBuilder
            {
                Title = command.Name,
                Description = Locate(command.Summary ?? "HelpNoDescription"),
                Color = new Discord.Color(FergunConfig.EmbedColor)
            };

            if (command.Parameters.Count > 0)
            {
                // Add parameters: param1 (type) (Optional): description
                string field = "";
                foreach (var parameter in command.Parameters)
                {
                    field += $"{parameter.Name} ({parameter.Type.CSharpName()}) ";
                    if (parameter.IsOptional)
                        field += $" {Locate("HelpOptional")}";
                    field += $": {Locate(parameter.Summary ?? "HelpNoDescription")}\n";
                }
                builder.AddField(Locate("HelpParameters"), field);
            }

            // Add usage field (`prefix command <param1> [param2...]`)
            string usage = $"`{GetPrefix()}{command.Name}";
            foreach (var parameter in command.Parameters)
            {
                usage += " ";
                usage += parameter.IsOptional ? "[" : "<";
                usage += parameter.Name;
                if (parameter.IsRemainder || parameter.IsMultiple)
                    usage += "...";
                usage += parameter.IsOptional ? "]" : ">";
            }
            usage += "`";
            builder.AddField(Locate("HelpUsage"), usage);

            // Add notes if present
            if (!string.IsNullOrEmpty(command.Remarks))
            {
                builder.AddField(Locate("Notes"), Locate(command.Remarks));
            }

            // Add aliases if present
            if (command.Aliases.Count > 1)
            {
                builder.AddField(Locate("HelpAlias"), string.Join(", ", command.Aliases.Skip(1)));
            }

            // Add footer with info about obligatory and optional parameters
            if (command.Parameters.Count > 0)
            {
                builder.WithFooter(Locate("HelpFooter2"));
            }

            return builder.Build();
        }

        /// <summary>
        /// Gets the url from the last x messages / embeds /attachments.
        /// </summary>
        /// <param name="messagesToSearch">The number of messages to search.</param>
        /// <param name="onlyImage">Get only urls of images.</param>
        /// <param name="maxSize">The maximum file size in bytes, 8 MB by default.</param>
        /// <returns>The url on success, or null and the error reason.</returns>
        private async Task<(string, string)> GetLastUrl(int messagesToSearch, bool onlyImage, string url = null, long maxSize = 8000000)
        {
            long? size = null;
            //If the message that executed the command contains any suitable attachment or url
            if (url != null || Context.Message.Attachments.Count > 0)
            {
                if (Context.Message.Attachments.Count > 0)
                {
                    var attachment = Context.Message.Attachments.First();
                    if (onlyImage && attachment.Width == null && attachment.Height == null)
                    {
                        return (null, "AttachNotImage");
                    }
                    url = attachment.Url;
                }
                if (onlyImage && !await IsImageUrlAsync(url))
                {
                    return (null, "UrlNotImage");
                }
                size = await GetUrlContentLengthAsync(url);
                if (size != null && size > maxSize)
                {
                    return (null, "ImageTooLarge");
                }
                return (url, null);
            }

            // A regex i copied and pasted from somewhere (yep)
            Regex linkParser = new Regex(@"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            //Get the last x messages of the current channel
            var messages = await Context.Channel.GetMessagesAsync(messagesToSearch).FlattenAsync();

            //Try to get the last message with any attachment, embed image url or that contains an url
            var filtered = messages.FirstOrDefault(x =>
            x.Attachments.Any(y => !onlyImage || y.Width != null && y.Height != null)
            || x.Embeds.Any(y => !onlyImage || y.Image != null || y.Thumbnail != null)
            || linkParser.IsMatch(x.Content));

            //If there's no results, return nothing
            if (filtered == null)
            {
                return (null, "ImageNotFound");
            }

            //Note: attachments and embeds can contain text but i'm prioritizing the previous ones
            // Priority order: attachments > embeds > text
            if (filtered.Attachments.Count > 0)
            {
                //var attachment = filtered.Attachments.First();
                //if (OnlyImage && attachment.Width != null && attachment.Height != null)//!IsImageUrl(filtered.Attachments.First().Url))
                //{
                //    return (null, "ImageNotFound");
                //}
                url = filtered.Attachments.First().Url;
                size = filtered.Attachments.First().Size;
            }
            else if (filtered.Embeds.Count > 0)
            {
                var embed = filtered.Embeds.First();
                var image = embed.Image;
                var thumbnail = embed.Thumbnail;
                if (onlyImage)
                {
                    if (image?.Height != null && image?.Width != null)
                    {
                        url = image?.Url;
                    }
                    else if (thumbnail?.Height != null && thumbnail?.Width != null)
                    {
                        url = thumbnail?.Url;
                    }
                    else
                    {
                        return (null, "ImageNotFound");
                    }
                    //string ImageUrl = embed.Image.HasValue ? embed.Image.Value.Url : embed.Thumbnail.Value.Url;
                    //if (!IsImageUrl(ImageUrl))
                    //{
                    //    return (null, "ImageNotFound");
                    //}
                    //else
                    //{
                    //    url = ImageUrl;
                    //}
                }
                else
                {
                    url = embed.Url ?? image?.Url ?? thumbnail?.Url;
                }
            }
            else
            {
                string match = linkParser.Match(filtered.Content).Value;
                if (onlyImage && !await IsImageUrlAsync(match))
                {
                    return (null, "ImageNotFound");
                }
                url = match;
            }
            // Not null if the url was from an attachment.
            if (size == null)
            {
                size = await GetUrlContentLengthAsync(url);
            }
            if (size != null && size > maxSize)
            {
                return (null, "ImageTooLarge");
            }
            return (url, null);
        }

        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[RngInstance.Next(s.Length)]).ToArray());
        }
    }

    public class CachedPages
    {
        public CachedPages(string query, List<PaginatedMessage.Page> pages, bool isNsfw)
        {
            Query = query;
            Pages = pages;
            IsNsfw = isNsfw;
        }

        public bool IsNsfw { get; set; }
        public List<PaginatedMessage.Page> Pages { get; set; }
        public string Query { get; set; }
    }

    public class SimpleTranslationResult
    {
        public string Error { get; set; }
        public Language Source { get; set; }
        public Language Target { get; set; }
        public string Text { get; set; }
    }

    public class CachedTts
    {
        public CachedTts(string language, string text, byte[] tts)
        {
            Language = language;
            Text = text;
            Tts = tts;
        }

        public string Language { get; set; }
        public string Text { get; set; }
        public byte[] Tts { get; set; }
    }
}