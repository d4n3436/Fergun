using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fergun.Hardware;

internal sealed class UnknownHardwareInfo : IHardwareInfo
{
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