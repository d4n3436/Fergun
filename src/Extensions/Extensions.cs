using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Fergun.Extensions;

public static class Extensions
{
    private static readonly JsonSerializerOptions _cachedOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

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

        displayMessage += context.Channel?.Name ?? (context.Interaction as SocketInteraction)?.InteractionChannel?.Name ?? $"??? (Id: {context.Interaction.ChannelId})";

        return displayMessage;
    }

    [UsedImplicitly]
    public static string Dump<T>(this T obj) => JsonSerializer.Serialize(obj, _cachedOptions);
}