using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Fergun.Common;

/// <summary>
/// Represents the mobile patcher.
/// </summary>
public static class MobilePatcher
{
    /// <summary>
    /// Patches Discord.Net to display the mobile status.
    /// </summary>
    public static void Patch()
    {
        var harmony = new Harmony(nameof(MobilePatcher));
        var original = AccessTools.Method("Discord.API.DiscordSocketApiClient:SendGatewayAsync");

        harmony.Patch(original, new HarmonyMethod(Prefix));
    }

    private static void Prefix(byte opCode, object payload)
    {
        if (opCode != 2) // Identify
            return;

        var properties = GetProperties(payload);

        if (!properties.TryGetValue("$device", out string? device)
            || device != "Discord.Net")
        {
            return;
        }

        properties["$os"] = "android";
        properties["$browser"] = "Discord Android";
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Properties")]
    private static extern IDictionary<string, string> GetProperties([UnsafeAccessorType("Discord.API.Gateway.IdentifyParams, Discord.Net.WebSocket")] object instance);
}