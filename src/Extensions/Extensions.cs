using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Victoria;

namespace Fergun.Extensions
{
    public static class Extensions
    {
        // Copy pasted from SocketGuildUser Hierarchy property to be used with RestGuildUser
        public static int GetHierarchy(this IGuildUser user)
        {
            if (user.Guild.OwnerId == user.Id)
            {
                return int.MaxValue;
            }

            int maxPos = 0;
            for (int i = 0; i < user.RoleIds.Count; i++)
            {
                var role = user.Guild.GetRole(user.RoleIds.ElementAt(i));
                if (role != null && role.Position > maxPos)
                {
                    maxPos = role.Position;
                }
            }

            return maxPos;
        }

        public static void Shuffle<T>(this IList<T> list, Random rng = null)
        {
            rng ??= new Random();

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static string GetFriendlyName(this Type type)
        {
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(byte))
                return "byte";
            if (type == typeof(sbyte))
                return "sbyte";
            if (type == typeof(short))
                return "short";
            if (type == typeof(ushort))
                return "ushort";
            if (type == typeof(char))
                return "char";
            if (type == typeof(int))
                return "int";
            if (type == typeof(uint))
                return "uint";
            if (type == typeof(long))
                return "long";
            if (type == typeof(ulong))
                return "ulong";
            if (type == typeof(float))
                return "float";
            if (type == typeof(double))
                return "double";
            if (type == typeof(decimal))
                return "decimal";
            if (type == typeof(string))
                return "string";
            if (type == typeof(object))
                return "object";

            if (!type.IsGenericType) return type.Name;

            string arguments = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName).ToArray());
            if (type.Name.Contains("Nullable", StringComparison.OrdinalIgnoreCase))
            {
                return arguments + "?";
            }

            return $"{type.Name.Split('`')[0]}<{arguments}>";
        }

        public static string FileExtensionFromEncoder(this System.Drawing.Imaging.ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(x => x.FormatID == format.Guid)
                ?.FilenameExtension
                ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim('*')
                .ToLowerInvariant() ?? ".jpg";
        }

        public static string ToTrackLink(this LavaTrack track, bool withTime = true)
        {
            return Format.Url(track.Title, track.Url) + (withTime ? $" ({track.Duration.ToShortForm()})" : "");
        }

        public static Embed ToHelpEmbed(this CommandInfo command, string language, string prefix)
        {
            var builder = new EmbedBuilder
            {
                Title = command.Name,
                Description = GuildUtils.Locate(command.Summary ?? "NoDescription", language),
                Color = new Color(FergunClient.Config.EmbedColor)
            };

            if (command.Parameters.Count > 0)
            {
                // Add parameters: param1 (type) (Optional): description
                var field = new StringBuilder();
                foreach (var parameter in command.Parameters)
                {
                    field.Append($"{parameter.Name} ({parameter.Type.GetFriendlyName()})");
                    if (parameter.IsOptional)
                    {
                        field.Append(' ');
                        field.Append(GuildUtils.Locate("Optional", language));
                    }

                    field.Append($": {GuildUtils.Locate(parameter.Summary ?? "NoDescription", language)}\n");
                }
                builder.AddField(GuildUtils.Locate("Parameters", language), field.ToString());
            }

            // Add usage field (`prefix group command <param1> [param2...]`)
            var usage = new StringBuilder('`' + prefix);
            if (!string.IsNullOrEmpty(command.Module.Group))
            {
                usage.Append(command.Module.Group);
                usage.Append(' ');
            }
            usage.Append(command.Name);
            foreach (var parameter in command.Parameters)
            {
                usage.Append(' ');
                usage.Append(parameter.IsOptional ? '[' : '<');
                usage.Append(parameter.Name);
                if (parameter.IsRemainder || parameter.IsMultiple)
                {
                    usage.Append("...");
                }

                usage.Append(parameter.IsOptional ? ']' : '>');
            }
            usage.Append('`');
            builder.AddField(GuildUtils.Locate("Usage", language), usage.ToString());

            // Add example if the command has parameters
            if (command.Parameters.Count > 0)
            {
                var attribute = command.Attributes.OfType<ExampleAttribute>().FirstOrDefault();
                if (attribute != null)
                {
                    string example = prefix;
                    if (!string.IsNullOrEmpty(command.Module.Group))
                    {
                        example += command.Module.Group + ' ';
                    }
                    example += $"{command.Name} {attribute.Example}";
                    builder.AddField(GuildUtils.Locate("Example", language), example);
                }
            }

            // Add notes if present
            if (!string.IsNullOrEmpty(command.Remarks))
            {
                builder.AddField(GuildUtils.Locate("Notes", language), GuildUtils.Locate(command.Remarks, language));
            }

            var modulePreconditions = command.Module.Preconditions;
            var commandPreconditions = command.Preconditions;

            // Add ratelimit info if present
            var ratelimit = commandPreconditions.Concat(modulePreconditions).OfType<RatelimitAttribute>().FirstOrDefault();

            if (ratelimit != null)
            {
                builder.AddField("Ratelimit", string.Format(GuildUtils.Locate("RatelimitUses", language), ratelimit.InvokeLimit, ratelimit.InvokeLimitPeriod.ToShortForm2()));
            }

            // Add required permissions if there's any
            var preconditions = modulePreconditions
                .Concat(commandPreconditions).Where(x => !(x is RatelimitAttribute) && !(x is LongRunningAttribute) && !(x is DisabledAttribute));

            var list = new StringBuilder();
            foreach (var precondition in preconditions)
            {
                string name = precondition.GetType().Name;
                list.Append(Format.Code(name.Substring(0, name.Length - 9)));
                switch (precondition)
                {
                    case RequireContextAttribute requireContext:
                        list.Append($": {requireContext.Contexts}");
                        break;

                    case RequireUserPermissionAttribute requireUser:
                        list.Append($": {requireUser.GuildPermission?.ToString() ?? requireUser.ChannelPermission?.ToString()}");
                        break;

                    case RequireBotPermissionAttribute requireBot:
                        list.Append($": {requireBot.GuildPermission?.ToString() ?? requireBot.ChannelPermission?.ToString()}");
                        break;
                }
                list.Append('\n');
            }

            if (list.Length != 0)
            {
                builder.AddField(GuildUtils.Locate("Requirements", language), list.ToString());
            }

            // Add aliases if present
            if (command.Aliases.Count > 1)
            {
                builder.AddField(GuildUtils.Locate("Alias", language), string.Join(", ", command.Aliases.Skip(1)));
            }

            // Add footer with info about required and optional parameters
            if (command.Parameters.Count > 0)
            {
                builder.WithFooter(GuildUtils.Locate("HelpFooter2", language));
            }

            return builder.Build();
        }

        /// <summary>
        /// Gets the last url in the last <paramref name="messageCount"/> messages.
        /// </summary>
        /// <param name="context">The channel to search.</param>
        /// <param name="messageCount">The number of messages to search.</param>
        /// <param name="onlyImage">Get only urls of images.</param>
        /// <param name="url">An optional url to use before searching in the channel.</param>
        /// <param name="maxSize">The maximum file size in bytes, <see cref="Constants.AttachmentSizeLimit"/> by default.</param>
        /// <returns>A task that represents an asynchronous search operation.</returns>
        public static Task<(string, UrlFindResult)> GetLastUrlAsync(this ICommandContext context, int messageCount, bool onlyImage = false,
            string url = null, long maxSize = Constants.AttachmentSizeLimit)
        {
            return context.Channel.GetLastUrlAsync(messageCount, onlyImage, context.Message, url, maxSize);
        }

        public static bool IsNsfw(this ICommandContext context)
        {
            // Considering a DM channel a SFW channel.
            return context.Channel is ITextChannel textChannel && textChannel.IsNsfw;
        }

        public static string Display(this ICommandContext context, bool displayUser = false)
        {
            return context.Channel.Display() + (displayUser ? $"/{context.User}" : "");
        }

        public static string Display(this IChannel channel)
        {
            return (channel is IGuildChannel guildChannel ? $"{guildChannel.Guild.Name}/" : "") + channel.Name;
        }

        public static string Display(this IMessage message)
        {
            return message.Channel.Display() + $"/{message.Author}";
        }

        public static string Dump<T>(this T obj, int maxDepth = 2)
        {
            try
            {
                using var strWriter = new StringWriter();
                using var jsonWriter = new CustomJsonTextWriter(strWriter);
                var resolver = new CustomContractResolver(() => jsonWriter.CurrentDepth <= maxDepth);
                var serializer = new JsonSerializer
                {
                    ContractResolver = resolver,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                serializer.Serialize(jsonWriter, obj);
                return strWriter.ToString();
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }
    }

    public class CustomJsonTextWriter : JsonTextWriter
    {
        public CustomJsonTextWriter(TextWriter textWriter) : base(textWriter)
        {
        }

        public int CurrentDepth { get; private set; }

        public override void WriteStartObject()
        {
            CurrentDepth++;
            base.WriteStartObject();
        }

        public override void WriteEndObject()
        {
            CurrentDepth--;
            base.WriteEndObject();
        }
    }

    public class CustomContractResolver : DefaultContractResolver
    {
        private readonly Func<bool> _includeProperty;

        public CustomContractResolver(Func<bool> includeProperty)
        {
            _includeProperty = includeProperty;
        }

        protected override JsonProperty CreateProperty(
            MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => _includeProperty() &&
                                              (shouldSerialize == null ||
                                               shouldSerialize(obj));
            return property;
        }
    }
}