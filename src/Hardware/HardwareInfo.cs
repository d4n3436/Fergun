using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fergun.Hardware;

/// <summary>
/// Contains static methods to get information about the system hardware.
/// </summary>
public static class HardwareInfo
{
    private static readonly Lazy<string?> _lazyCpuName;
    private static readonly Lazy<string> _lazyOsName;

    static HardwareInfo()
    {
        if (OperatingSystem.IsWindows())
            Instance = new WindowsHardwareInfo();
        else if (OperatingSystem.IsLinux())
            Instance = new LinuxHardwareInfo();
        else if (OperatingSystem.IsMacOS())
            Instance = new MacOsHardwareInfo();
        else
            Instance = new UnknownHardwareInfo();

        _lazyCpuName = new Lazy<string?>(() => Instance.GetCpuName(), true);
        _lazyOsName = new Lazy<string>(() => Instance.GetOperatingSystemName(), true);
    }

    /// <summary>
    /// Gets the inner (OS-specific) <see cref="IHardwareInfo"/> instance.
    /// </summary>
    public static IHardwareInfo Instance { get; }

    /// <inheritdoc cref="IHardwareInfo.GetCpuName"/>
    public static string? GetCpuName() => _lazyCpuName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetOperatingSystemName"/>
    public static string GetOperatingSystemName() => _lazyOsName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetMemoryStatus"/>
    public static MemoryStatus GetMemoryStatus() => Instance.GetMemoryStatus();

    /// <summary>
    /// Gets the CPU usage for the current process.
    /// </summary>
    /// <returns>The CPU usage in a the range 0 to 1.</returns>
    public static async Task<double> GetCpuUsageAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        await Task.Delay(500);

        var endTime = DateTimeOffset.UtcNow;
        var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        double cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        double totalMsPassed = (endTime - startTime).TotalMilliseconds;
        double cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        return cpuUsageTotal;
    }
}