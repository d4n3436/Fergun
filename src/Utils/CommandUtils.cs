using System;
using System.Diagnostics;

namespace Fergun.Utils;

public static class CommandUtils
{
    public static string? RunCommand(string command)
    {
        bool isLinux = OperatingSystem.IsLinux();
        bool isWindows = OperatingSystem.IsWindows();
        if (!isLinux && !isWindows)
            return null;

        string escapedArgs = command.Replace("\"", "\\\"", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = isLinux ? "/bin/bash" : "cmd.exe",
            Arguments = isLinux ? $"-c \"{escapedArgs}\"" : $"/c {escapedArgs}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = isLinux ? "/home" : string.Empty
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit(10000);

        return process.ExitCode == 0
            ? process.StandardOutput.ReadToEnd()
            : process.StandardError.ReadToEnd();
    }
}