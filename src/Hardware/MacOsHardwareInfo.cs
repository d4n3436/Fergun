using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface through macOS-specific APIs.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsHardwareInfo : IHardwareInfo
{
    internal MacOsHardwareInfo()
    {
    }

    private const int ENOMEM = 12;
    private const string CpuNameKey = "machdep.cpu.brand_string";
    private const string MemSizeKey = "hw.memsize";

    /// <inheritdoc />
    public string? GetCpuName()
    {
        ulong length = 0;

        if (sysctlbyname(CpuNameKey, null, ref length, null, 0) is not 0 or ENOMEM)
            return null;

        byte[] buffer = new byte[length];
        int code = sysctlbyname(CpuNameKey, buffer, ref length, null, 0);
        return code == 0 ? Encoding.UTF8.GetString(buffer.AsSpan(0, buffer.Length - 1)) : null;
    }

    /// <inheritdoc />
    public string GetOperatingSystemName() => $"macOS {Environment.OSVersion.Version}";

    /// <inheritdoc />
    public MemoryStatus GetMemoryStatus()
    {
        long totalRam = 0;
        ulong length = (uint)Marshal.SizeOf(totalRam);
        byte[] buffer = new byte[length];

        if (sysctlbyname(MemSizeKey, buffer, ref length, null, 0) == 0)
        {
            totalRam = BitConverter.ToInt64(buffer);
        }

        return new MemoryStatus
        {
            TotalPhysicalMemory = totalRam,
            ProcessUsedMemory = Process.GetCurrentProcess().WorkingSet64
        };
    }

    [DllImport("libc", BestFitMapping = false, CharSet = CharSet.Ansi, ExactSpelling = true, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [SupportedOSPlatform("macos")]
    private static extern int sysctlbyname([In, MarshalAs(UnmanagedType.LPStr)] string name, [In, Out] byte[]? oldp, ref ulong oldlenp, [In, Out] byte[]? newp, ulong newlen);
}