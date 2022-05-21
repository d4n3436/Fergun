using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fergun.Utils;

public static class CommandUtils
{
    public static async Task<double> GetCpuUsageForProcessAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        await Task.Delay(500);

        var endTime = DateTimeOffset.UtcNow;
        var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        return cpuUsageTotal * 100;
    }

    public static string? RunCommand(string command)
    {
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (!isLinux && !isWindows)
            return null;

        var escapedArgs = command.Replace("\"", "\\\"", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = isLinux ? "/bin/bash" : "cmd.exe",
            Arguments = isLinux ? $"-c \"{escapedArgs}\"" : $"/c {escapedArgs}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = isLinux ? "/home" : ""
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit(10000);

        return process.ExitCode == 0
            ? process.StandardOutput.ReadToEnd()
            : process.StandardError.ReadToEnd();
    }
}