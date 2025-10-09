using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Fergun.Utils;

public static class CommandUtils
{
    public static async Task<string?> StartProcessAsync(string fileName, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(cts.Token);

        return await process.StandardOutput.ReadToEndAsync(cts.Token);
    }
}