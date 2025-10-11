using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fergun.Hardware;

/// <summary>
/// Contains static methods to retrieve information about the system hardware.
/// </summary>
public static class HardwareInfo
{
    private static readonly Lazy<IHardwareInfo> _lazyInstance = new(InitializeInstance);
    private static readonly Lazy<string?> _lazyCpuName = new(Instance.GetCpuName);
    private static readonly Lazy<string> _lazyOsName = new(Instance.GetOperatingSystemName);

    /// <summary>
    /// Gets the inner (OS-specific) <see cref="IHardwareInfo"/> instance.
    /// </summary>
    public static IHardwareInfo Instance => _lazyInstance.Value;

    /// <inheritdoc cref="IHardwareInfo.GetCpuName"/>
    public static string? CpuName => _lazyCpuName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetOperatingSystemName"/>
    public static string OperatingSystemName => _lazyOsName.Value;

    /// <inheritdoc cref="IHardwareInfo.GetMemoryStatus"/>
    public static MemoryStatus GetMemoryStatus() => _lazyInstance.Value.GetMemoryStatus();

    /// <summary>
    /// Gets the CPU usage for the current process.
    /// </summary>
    /// <returns>The CPU usage in the range 0 to 1.</returns>
    public static async Task<double> GetCpuUsageAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        await Task.Delay(500);

        var endTime = DateTimeOffset.UtcNow;
        var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        double cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        double totalMsPassed = (endTime - startTime).TotalMilliseconds;
        return cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
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