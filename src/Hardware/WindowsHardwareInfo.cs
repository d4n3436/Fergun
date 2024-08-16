using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface through Windows-specific APIs.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class WindowsHardwareInfo : IHardwareInfo
{
    internal WindowsHardwareInfo()
    {
    }

    /// <inheritdoc/>
    public string? GetCpuName()
    {
        string? cpuName = null;

        using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        using var objects = searcher.Get();

        foreach (var mo in objects)
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

    [SupportedOSPlatform("windows")]
    [LibraryImport("KERNEL32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

[StructLayout(LayoutKind.Sequential)]
internal ref struct MEMORYSTATUSEX
{
    public uint dwLength;
    public uint dwMemoryLoad = default;
    public ulong ullTotalPhys = default;
    public ulong ullAvailPhys = default;
    public ulong ullTotalPageFile = default;
    public ulong ullAvailPageFile = default;
    public ulong ullTotalVirtual = default;
    public ulong ullAvailVirtual = default;
    public ulong ullAvailExtendedVirtual = default;

    public unsafe MEMORYSTATUSEX() => dwLength = (uint)sizeof(MEMORYSTATUSEX);
}