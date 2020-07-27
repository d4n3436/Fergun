using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.Commands;
using Fergun.Attributes;
using Fergun.Services;
using Microsoft.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Victoria;

namespace Fergun.Extensions
{
    public static class Extension
    {
        // Copy pasted from SocketGuildUser Hiearchy property to be used with RestGuildUser
        public static int GetHierarchy(this IGuildUser user)
        {
            if (user.Guild.OwnerId == user.Id)
            {
                return int.MaxValue;
            }

            int num = 0;
            for (int i = 0; i < user.Guild.Roles.Count; i++)
            {
                IRole role = user.Guild.Roles.ElementAt(i);
                if (role != null && role.Position > num)
                {
                    num = role.Position;
                }
            }

            return num;
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

        public static string CSharpName(this Type type)
        {
            if (!type.FullName.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return type.Name;
            string output;
            using (var compiler = new CSharpCodeProvider())
            {
                var t = new CodeTypeReference(type);
                output = compiler.GetTypeOutput(t);
            }
            output = output.Replace("System.", "", StringComparison.OrdinalIgnoreCase);
            if (output.Contains("Nullable<", StringComparison.OrdinalIgnoreCase))
                output = output
                    .Replace("Nullable", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(">", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("<", "", StringComparison.OrdinalIgnoreCase) + "?";
            return output;
        }

        public static string FileExtensionFromEncoder(this System.Drawing.Imaging.ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders()
                                 .FirstOrDefault(x => x.FormatID == format.Guid)?
                                 .FilenameExtension?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)?
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
            //Maybe there's a better way to make a dynamic help?
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
                    field += $"{parameter.Name} ({parameter.Type.CSharpName()})";
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
                var attribute = command.Attributes.FirstOrDefault(x => x.GetType() == typeof(ExampleAttribute));
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

            // Add aliases if present
            if (command.Aliases.Count > 1)
            {
                builder.AddField(Localizer.Locate("Alias", language), string.Join(", ", command.Aliases.Skip(1)));
            }

            // Add footer with info about obligatory and optional parameters
            if (command.Parameters.Count > 0)
            {
                builder.WithFooter(Localizer.Locate("HelpFooter2", language));
            }

            return builder.Build();
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
            catch
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