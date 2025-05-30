using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface through macOS-specific APIs.
/// </summary>
[SupportedOSPlatform("macos")]
public partial class MacOsHardwareInfo : IHardwareInfo
{
    private const int ENOMEM = 12;

    private static ReadOnlySpan<byte> CpuNameKey => "machdep.cpu.brand_string"u8;

    private static ReadOnlySpan<byte> MemSizeKey => "hw.memsize"u8;

    internal MacOsHardwareInfo()
    {
    }

    /// <inheritdoc />
    public string? GetCpuName()
    {
        int length = 0;

        if (sysctlbyname(CpuNameKey, null, ref length, null, 0) is not (0 or ENOMEM))
            return null;

        Span<byte> buffer = new byte[length];
        int code = sysctlbyname(CpuNameKey, buffer, ref length, null, 0);
        return code == 0 ? Encoding.UTF8.GetString(buffer[..^1]) : null;
    }

    /// <inheritdoc />
    public string GetOperatingSystemName() => $"macOS {Environment.OSVersion.Version}";

    /// <inheritdoc />
    public MemoryStatus GetMemoryStatus()
    {
        long totalRam = 0;
        int length = sizeof(long);
        Span<byte> buffer = stackalloc byte[length];

        if (sysctlbyname(MemSizeKey, buffer, ref length, null, 0) == 0)
        {
            totalRam = BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        return new MemoryStatus
        {
            TotalPhysicalMemory = totalRam,
            ProcessUsedMemory = Process.GetCurrentProcess().WorkingSet64
        };
    }

    [SupportedOSPlatform("macos")]
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int sysctlbyname(ReadOnlySpan<byte> name, Span<byte> oldp, ref int oldlenp, Span<byte> newp, int newlen);
}