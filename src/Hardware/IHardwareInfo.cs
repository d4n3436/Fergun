namespace Fergun.Hardware;

/// <summary>
/// Provides methods to retrieve information about the system hardware.
/// </summary>
public interface IHardwareInfo
{
    /// <summary>
    /// Gets the CPU name.
    /// </summary>
    /// <returns>The CPU name, or <see langword="null"/> if it's not available.</returns>
    string? GetCpuName();

    /// <summary>
    /// Gets the name of the operating system (or the distribution's name on Linux if possible).
    /// </summary>
    /// <returns>The name of the operating system or distribution.</returns>
    string GetOperatingSystemName();

    /// <summary>
    /// Gets information of the current memory state of the system.
    /// </summary>
    /// <returns>The information of the current memory state of the system.</returns>
    MemoryStatus GetMemoryStatus();
}