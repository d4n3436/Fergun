using System.Globalization;
using System.Linq;
using System.Text;
using Discord;

namespace Fergun.Extensions;

public static class MessageExtensions
{
    public static string GetText(this IMessage message)
    {
        var builder = new StringBuilder(message.Content, message.Content.Length);

        if (message.Embeds.Count > 0)
        {
            var embed = message.Embeds.First();

            builder.Append($"\n{embed.Author?.Name}\n{embed.Title}\n{embed.Description}\n");

            foreach (var field in embed.Fields)
            {
                string name = field.Name.Trim().Replace("\u200b", string.Empty);
                string value = field.Value.Trim().Replace("\u200b", string.Empty);
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                {
                    builder.Append(CultureInfo.InvariantCulture, $"{name}: {value}\n");
                }
            }

            builder.Append(embed.Footer?.Text);
        }

        return builder.ToString();
    }
}