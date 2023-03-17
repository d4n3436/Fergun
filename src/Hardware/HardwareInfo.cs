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
    private static readonly IHardwareInfo _instance;
    
    static HardwareInfo()
    {
        if (OperatingSystem.IsWindows())
            _instance = new WindowsHardwareInfo();
        else if (OperatingSystem.IsLinux())
            _instance = new LinuxHardwareInfo();
        else if (OperatingSystem.IsMacOS())
            _instance = new MacOsHardwareInfo();
        else
            _instance = new UnknownHardwareInfo();

        _lazyCpuName = new Lazy<string?>(() => _instance.GetCpuName(), true);
        _lazyOsName = new Lazy<string>(() => _instance.GetOperatingSystemName(), true);
    }

    /// <inheritdoc cref="IHardwareInfo.GetCpuName"/>
    public static string? GetCpuName() => _lazyCpuName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetOperatingSystemName"/>
    public static string GetOperatingSystemName() => _lazyOsName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetMemoryStatus"/>
    public static MemoryStatus GetMemoryStatus() => _instance.GetMemoryStatus();

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