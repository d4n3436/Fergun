using System;
using System.Collections.Generic;
using System.Reflection;
using Discord.WebSocket;
using HarmonyLib;

namespace Fergun;

/// <summary>
/// Represents the mobile patcher.
/// </summary>
public static class MobilePatcher
{
    private static readonly Type _identifyParams =
        typeof(BaseSocketClient).Assembly.GetType("Discord.API.Gateway.IdentifyParams", true)!;

    private static readonly PropertyInfo? _property = _identifyParams.GetProperty("Properties");

    /// <summary>
    /// Patches Discord.Net to display the mobile status.
    /// </summary>
    public static void Patch()
    {
        var harmony = new Harmony(nameof(MobilePatcher));

        var original = AccessTools.Method("Discord.API.DiscordSocketApiClient:SendGatewayAsync");
        var prefix = typeof(MobilePatcher).GetMethod(nameof(Prefix));

        harmony.Patch(original, new HarmonyMethod(prefix));
    }

    public static void Prefix(byte opCode, object payload)
    {
        if (opCode != 2) // Identify
            return;

        if (payload.GetType() != _identifyParams)
            return;

        if (_property?.GetValue(payload) is not IDictionary<string, string> props
            || !props.TryGetValue("$device", out string? device)
            || device != "Discord.Net")
        {
            return;
        }

        props["$os"] = "android";
        props["$browser"] = "Discord Android";
    }
}