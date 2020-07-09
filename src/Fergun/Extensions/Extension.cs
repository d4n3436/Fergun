using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Fergun.Extensions
{
    public static class Extension
    {
        /// <summary>
        /// Try to delete a message.
        /// </summary>
        /// <param name="messageId">The message Id.</param>
        public static async Task<bool> TryDeleteMessageAsync(this ISocketMessageChannel channel, ulong messageId)
        {
            var msg = await channel.GetMessageAsync(messageId);
            return await TryDeleteMessageInternalAsync(msg);
        }

        /// <summary>
        /// Try to delete a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public static async Task<bool> TryDeleteMessageAsync(this ISocketMessageChannel channel, IMessage message)
        {
            var msg = await channel.GetMessageAsync(message.Id);
            return await TryDeleteMessageInternalAsync(msg);
        }

        /// <summary>
        /// Try to delete this message.
        /// </summary>
        public static async Task<bool> TryDeleteAsync(this IMessage message)
        {
            if (message == null) return false;
            var msg = await message.Channel.GetMessageAsync(message.Id);
            return await TryDeleteMessageInternalAsync(msg);
        }

        internal static async Task<bool> TryDeleteMessageInternalAsync(IMessage message)
        {
            if (message == null)
            {
                // The message is already deleted.
                return false;
            }

            if (message.Channel is SocketGuildChannel guildChannel)
            {
                if (!guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages)
                {
                    // Missing permissions
                    return false;
                }
            }
            else
            {
                // Not possible to delete other user's messages in DM
                if (message.Source == MessageSource.User) return false;
            }

            try
            {
                await message.DeleteAsync();
                return true;
            }
            catch (HttpException)
            {
                return false;
            }
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

        public static string Dump(this object obj, int maxDepth = 2)
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