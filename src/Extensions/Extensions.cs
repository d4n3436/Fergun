using System;
using System.IO;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Fergun.Extensions;

public static class Extensions
{
    public static LogLevel ToLogLevel(this LogSeverity logSeverity)
        => logSeverity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(logSeverity))
        };

    public static string Display(this IInteractionContext context)
    {
        string displayMessage = string.Empty;

        if (context.Channel is IGuildChannel guildChannel)
            displayMessage = $"{guildChannel.Guild.Name}/";

        displayMessage += context.Channel?.Name ?? $"??? (Id: {context.Interaction.ChannelId})";

        return displayMessage;
    }

    public static string Dump<T>(this T obj, int maxDepth = 2)
    {
        using var strWriter = new StringWriter();
        strWriter.NewLine = "\n";
        using var jsonWriter = new CustomJsonTextWriter(strWriter);
        var resolver = new CustomContractResolver(jsonWriter, maxDepth);
        var serializer = new JsonSerializer
        {
            ContractResolver = resolver,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        };
        serializer.Serialize(jsonWriter, obj);
        return strWriter.ToString();
    }
}