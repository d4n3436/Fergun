using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface through Windows-specific APIs.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsHardwareInfo : IHardwareInfo
{
    internal WindowsHardwareInfo()
    {
    }

    /// <inheritdoc/>
    public string? GetCpuName()
    {
        string? cpuName = null;

        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");

        foreach (var mo in searcher.Get())
        {
            cpuName = mo["Name"].ToString();
        }

        return cpuName;
    }

    /// <inheritdoc/>
    public string GetOperatingSystemName() => RuntimeInformation.OSDescription;

    /// <inheritdoc/>
    public MemoryStatus GetMemoryStatus()
    {
        var memoryStatus = new MEMORYSTATUSEX();
        long totalMemory = 0;
        long availableMemory = 0;

        if (GlobalMemoryStatusEx(ref memoryStatus))
        {
            totalMemory = (long)memoryStatus.ullTotalPhys;
            availableMemory = (long)memoryStatus.ullAvailPhys;
        }

        return new MemoryStatus
        {
            TotalPhysicalMemory = totalMemory,
            AvailablePhysicalMemory = availableMemory,
            UsedPhysicalMemory = totalMemory - availableMemory,
            ProcessUsedMemory = Process.GetCurrentProcess().PrivateMemorySize64
        };
    }

    [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MEMORYSTATUSEX
{
    public MEMORYSTATUSEX() => dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

    public uint dwLength;
    public uint dwMemoryLoad = default;
    public ulong ullTotalPhys = default;
    public ulong ullAvailPhys = default;
    public ulong ullTotalPageFile = default;
    public ulong ullAvailPageFile = default;
    public ulong ullTotalVirtual = default;
    public ulong ullAvailVirtual = default;
    public ulong ullAvailExtendedVirtual = default;
}