namespace Fergun.Hardware;

/// <summary>
/// Contains information about the memory.
/// </summary>
public readonly struct MemoryStatus
{
    /// <summary>
    /// Gets the total physical, memory, in bytes.
    /// </summary>
    public long TotalPhysicalMemory { get; init; }

    /// <summary>
    /// Gets the available physical memory, in bytes.
    /// </summary>
    public long AvailablePhysicalMemory { get; init; }

    /// <summary>
    /// Gets the used physical memory, in bytes.
    /// </summary>
    public long UsedPhysicalMemory { get; init; }

    /// <summary>
    /// Gets the used physical memory by the current process, in bytes.
    /// </summary>
    public long ProcessUsedMemory { get; init; }
}