using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fergun.Hardware;

/// <summary>
/// Implements the <see cref="IHardwareInfo"/> interface with incomplete or generic values.
/// </summary>
public sealed class UnknownHardwareInfo : IHardwareInfo
{
    internal UnknownHardwareInfo()
    {
    }

    /// <inheritdoc />
    public string? GetCpuName() => null;

    /// <inheritdoc />
    public string GetOperatingSystemName() => RuntimeInformation.OSDescription;

    /// <inheritdoc />
    public MemoryStatus GetMemoryStatus() => new()
    {
        ProcessUsedMemory = Process.GetCurrentProcess().WorkingSet64
    };
}