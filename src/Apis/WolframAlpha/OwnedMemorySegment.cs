using System.Buffers;

namespace Fergun.Apis.WolframAlpha;

internal sealed class OwnedMemorySegment<T> : ReadOnlySequenceSegment<T>, IDisposable
{
    private readonly IMemoryOwner<T> _owner;

    public OwnedMemorySegment(IMemoryOwner<T> owner, ReadOnlyMemory<T> memory)
    {
        _owner = owner;
        Memory = memory;
    }

    public OwnedMemorySegment<T> Append(IMemoryOwner<T> owner, ReadOnlyMemory<T> memory)
    {
        var segment = new OwnedMemorySegment<T>(owner, memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };

        Next = segment;

        return segment;
    }

    /// <inheritdoc/>
    public void Dispose() => _owner.Dispose();
}