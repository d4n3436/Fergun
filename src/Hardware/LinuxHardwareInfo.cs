using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface through Linux-specific APIs.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxHardwareInfo : IHardwareInfo
{
    private const string CpuInfoPath = "/proc/cpuinfo";
    private const string OsReleasePath = "/etc/os-release";
    private const string MemInfoPath = "/proc/meminfo";

    internal LinuxHardwareInfo()
    {
    }

    /// <inheritdoc/>
    public string? GetCpuName()
    {
        const string modelName = "model name";
        const string modelName2 = "Model name:";

        string? cpuName = null;

        if (TryReadFileLines(CpuInfoPath, out string[] lines))
        {
            foreach (string line in lines)
            {
                if (line.StartsWith(modelName))
                {
                    cpuName = line.AsSpan(modelName.Length).Trim(": ").ToString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(cpuName) && TryGetProcessOutput("lscpu", null, out string? output))
        {
            foreach (var line in output.AsSpan().EnumerateLines())
            {
                if (line.StartsWith(modelName2))
                {
                    cpuName = line[modelName2.Length..].Trim().ToString();
                    break;
                }
            }
        }

        return cpuName;
    }

    /// <inheritdoc/>
    public string GetOperatingSystemName()
    {
        const string prettyNameId = "PRETTY_NAME=";

        string osName = string.Empty;
        if (TryReadFileLines(OsReleasePath, out string[] lines))
        {
            foreach (string line in lines)
            {
                if (line.StartsWith(prettyNameId))
                {
                    osName = line.AsSpan(prettyNameId.Length).Trim('"').ToString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(osName))
        {
            osName = RuntimeInformation.OSDescription;
        }

        return osName;
    }

    /// <inheritdoc/>
    public MemoryStatus GetMemoryStatus()
    {
        const string memTotalId = "MemTotal:";
        const string memAvailable = "MemAvailable:";

        long totalMemory = 0;
        long availableMemory = 0;

        if (TryReadFileLines(MemInfoPath, out string[] lines))
        {
            foreach (string line in lines)
            {
                GetMemInfoData(line, memTotalId, ref totalMemory);
                GetMemInfoData(line, memAvailable, ref availableMemory);
            }
        }

        return new MemoryStatus
        {
            TotalPhysicalMemory = totalMemory,
            AvailablePhysicalMemory = availableMemory,
            UsedPhysicalMemory = totalMemory - availableMemory,
            ProcessUsedMemory = Process.GetCurrentProcess().WorkingSet64
        };
    }

    private static void GetMemInfoData(ReadOnlySpan<char> line, ReadOnlySpan<char> start, ref long data)
    {
        if (!line.StartsWith(start))
        {
            return;
        }

        var sliced = line[start.Length..];
        if (sliced.EndsWith("kB"))
        {
            sliced = sliced[..^2];
        }

        if (long.TryParse(sliced.Trim(), NumberStyles.None, NumberFormatInfo.InvariantInfo, out long temp))
        {
            data = temp * 1024;
        }
    }

    private static bool TryReadFileLines(string path, out string[] lines)
    {
        if (File.Exists(path))
        {
            try
            {
                lines = File.ReadAllLines(path);
                return true;
            }
            catch
            {
                lines = [];
                return false;
            }
        }

        lines = [];
        return false;
    }

    private static bool TryGetProcessOutput(string name, string? arguments, [MaybeNullWhen(false)] out string output)
    {
        output = null;

        var processInfo = new ProcessStartInfo
        {
            FileName = name,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process is null)
            return false;

        if (!process.WaitForExit(10000))
            return false;

        if (process.ExitCode != 0)
            return false;

        output = process.StandardOutput.ReadToEnd();
        return true;
    }
}