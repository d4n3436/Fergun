﻿namespace Fergun.Extensions;

public static class TimestampExtensions
{
    public static string ToDiscordTimestamp(this DateTimeOffset dateTime, char style = 'f')
        => dateTime.ToUnixTimeSeconds().ToDiscordTimestamp(style);

    public static string ToDiscordTimestamp(this DateTimeOffset? dateTime, char style = 'f')
        => dateTime.GetValueOrDefault().ToDiscordTimestamp(style);

    public static string ToDiscordTimestamp(this ulong timestamp, char style = 'f')
        => ((long)timestamp).ToDiscordTimestamp(style);

    public static string ToDiscordTimestamp(this long timestamp, char style = 'f')
        => $"<t:{timestamp}:{style}>";
}