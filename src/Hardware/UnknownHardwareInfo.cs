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
    public string? CpuName => null;

    /// <inheritdoc />
    public string OperatingSystemName => RuntimeInformation.OSDescription;

    /// <inheritdoc />
    public MemoryStatus GetMemoryStatus() => new()
    {
        ProcessUsedMemory = Process.GetCurrentProcess().WorkingSet64
    };
}