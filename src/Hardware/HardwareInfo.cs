using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fergun.Hardware;

/// <summary>
/// Contains static methods to retrieve information about the system hardware.
/// </summary>
public static class HardwareInfo
{
    /// <summary>
    /// Gets the inner (OS-specific) <see cref="IHardwareInfo"/> instance.
    /// </summary>
    public static IHardwareInfo Instance { get; } = InitializeInstance();

    /// <inheritdoc cref="IHardwareInfo.CpuName"/>
    public static string? CpuName => Instance.CpuName;

    /// <inheritdoc cref="IHardwareInfo.OperatingSystemName"/>
    public static string OperatingSystemName => Instance.OperatingSystemName;

    /// <inheritdoc cref="IHardwareInfo.GetMemoryStatus"/>
    public static MemoryStatus GetMemoryStatus() => Instance.GetMemoryStatus();

    /// <summary>
    /// Gets the CPU usage for the current process.
    /// </summary>
    /// <returns>The CPU usage in the range 0 to 1.</returns>
    public static async Task<double> GetCpuUsageAsync()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var startCpuUsage = Environment.CpuUsage.TotalTime;
        await Task.Delay(500);

        var endCpuUsage = Environment.CpuUsage.TotalTime;
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        double cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        return cpuUsedMs / (Environment.ProcessorCount * elapsed.TotalMilliseconds);
    }

    private static IHardwareInfo InitializeInstance()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsHardwareInfo();
        if (OperatingSystem.IsLinux())
            return new LinuxHardwareInfo();
        if (OperatingSystem.IsMacOS())
            return new MacOsHardwareInfo();

        return new UnknownHardwareInfo();
    }
}