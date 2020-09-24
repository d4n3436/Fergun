using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.Commands;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Victoria;

namespace Fergun.Extensions
{
    public static class Extensions
    {
        // Copy pasted from SocketGuildUser Hiearchy property to be used with RestGuildUser
        public static int GetHierarchy(this IGuildUser user)
        {
            if (user.Guild.OwnerId == user.Id)
            {
                return int.MaxValue;
            }

            int maxPos = 0;
            for (int i = 0; i < user.RoleIds.Count; i++)
            {
                IRole role = user.Guild.GetRole(user.RoleIds.ElementAt(i));
                if (role != null && role.Position > maxPos)
                {
                    maxPos = role.Position;
                }
            }

            return maxPos;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            var rng = new Random();

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
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
            if (type.IsGenericType)
            {
                string arguments = string.Join(", ", type.GetGenericArguments().Select(x => GetFriendlyName(x)).ToArray());
                if (type.Name.Contains("Nullable", StringComparison.OrdinalIgnoreCase))
                {
                    return arguments + "?";
                }
                return $"{type.Name.Split('`')[0]}<{arguments}>";
            }
            return type.Name;
        }

        public static string FileExtensionFromEncoder(this System.Drawing.Imaging.ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders()
                                 .FirstOrDefault(x => x.FormatID == format.Guid)?
                                 .FilenameExtension?.Split(';', StringSplitOptions.RemoveEmptyEntries)?
                                 .FirstOrDefault()?
                                 .Trim('*')?
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
                Description = Localizer.Locate(command.Summary ?? "NoDescription", language),
                Color = new Color(FergunConfig.EmbedColor)
            };

            if (command.Parameters.Count > 0)
            {
                // Add parameters: param1 (type) (Optional): description
                string field = "";
                foreach (var parameter in command.Parameters)
                {
                    field += $"{parameter.Name} ({parameter.Type.GetFriendlyName()})";
                    if (parameter.IsOptional)
                        field += ' ' + Localizer.Locate("Optional", language);
                    field += $": {Localizer.Locate(parameter.Summary ?? "NoDescription", language)}\n";
                }
                builder.AddField(Localizer.Locate("Parameters", language), field);
            }

            // Add usage field (`prefix group command <param1> [param2...]`)
            string usage = '`' + prefix;
            if (!string.IsNullOrEmpty(command.Module.Group))
            {
                usage += command.Module.Group + ' ';
            }
            usage += command.Name;
            foreach (var parameter in command.Parameters)
            {
                usage += ' ';
                usage += parameter.IsOptional ? '[' : '<';
                usage += parameter.Name;
                if (parameter.IsRemainder || parameter.IsMultiple)
                    usage += "...";
                usage += parameter.IsOptional ? ']' : '>';
            }
            usage += '`';
            builder.AddField(Localizer.Locate("Usage", language), usage);

            // Add example if the command has parameters
            if (command.Parameters.Count > 0)
            {
                var attribute = command.Attributes.FirstOrDefault(x => x is ExampleAttribute);
                if (attribute != null)
                {
                    string example = prefix;
                    if (!string.IsNullOrEmpty(command.Module.Group))
                    {
                        example += command.Module.Group + ' ';
                    }
                    example += $"{command.Name} {(attribute as ExampleAttribute).Example}";
                    builder.AddField(Localizer.Locate("Example", language), example);
                }
            }

            // Add notes if present
            if (!string.IsNullOrEmpty(command.Remarks))
            {
                builder.AddField(Localizer.Locate("Notes", language), Localizer.Locate(command.Remarks, language));
            }

            var modulePreconds = command.Module.Preconditions;
            var commandPreconds = command.Preconditions;

            // Add ratelimit info if present
            var ratelimit = commandPreconds.Concat(modulePreconds).OfType<RatelimitAttribute>().FirstOrDefault();

            if (ratelimit != null)
            {
                builder.AddField("Ratelimit", string.Format(Localizer.Locate("RatelimitUses", language), ratelimit.InvokeLimit, ratelimit.InvokeLimitPeriod.ToShortForm2()));
            }

            // Add required permissions if there's any
            var preconditions = modulePreconds
                .Concat(commandPreconds).Where(x => !(x is RatelimitAttribute) && !(x is LongRunningAttribute) && !(x is DisabledAttribute));

            if (preconditions.Any())
            {
                string list = "";
                foreach (var precondition in preconditions)
                {
                    string name = precondition.GetType().Name;
                    list += Format.Code(name.Substring(0, name.Length - 9));
                    if (precondition is RequireContextAttribute requireContext)
                    {
                        list += ": " + requireContext.Contexts.ToString();
                    }
                    else if (precondition is RequireUserPermissionAttribute requireUser)
                    {
                        list += $": {requireUser.GuildPermission?.ToString() ?? requireUser.ChannelPermission?.ToString()}";
                    }
                    else if (precondition is RequireBotPermissionAttribute requireBot)
                    {
                        list += $": {requireBot.GuildPermission?.ToString() ?? requireBot.ChannelPermission?.ToString()}";
                    }
                    list += "\n";
                }
                builder.AddField(Localizer.Locate("Requirements", language), list);
            }

            // Add aliases if present
            if (command.Aliases.Count > 1)
            {
                builder.AddField(Localizer.Locate("Alias", language), string.Join(", ", command.Aliases.Skip(1)));
            }

            // Add footer with info about required and optional parameters
            if (command.Parameters.Count > 0)
            {
                builder.WithFooter(Localizer.Locate("HelpFooter2", language));
            }

            return builder.Build();
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
                using (var strWriter = new StringWriter())
                {
                    using (var jsonWriter = new CustomJsonTextWriter(strWriter))
                    {
                        var resolver = new CustomContractResolver(() => jsonWriter.CurrentDepth <= maxDepth);
                        var serializer = new JsonSerializer
                        {
                            ContractResolver = resolver,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                            Formatting = Formatting.Indented
                        };
                        serializer.Serialize(jsonWriter, obj);
                    }
                    return strWriter.ToString();
                }
                //return JsonConvert.SerializeObject(obj,
                //    new JsonSerializerSettings
                //    {
                //        Formatting = Formatting.Indented,
                //        MaxDepth = 1,
                //        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                //    });
            }
            catch (JsonSerializationException)
            {
                return "Error";
            }
        }
    }

    public class CustomJsonTextWriter : JsonTextWriter
    {
        public CustomJsonTextWriter(TextWriter textWriter) : base(textWriter) { }

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