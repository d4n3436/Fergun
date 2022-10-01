using System.Buffers;

namespace Fergun.Apis.WolframAlpha;

internal sealed class OwnedMemorySegment : ReadOnlySequenceSegment<byte>, IDisposable
{
    private readonly IMemoryOwner<byte> _owner;

    public OwnedMemorySegment(IMemoryOwner<byte> owner, ReadOnlyMemory<byte> memory)
    {
        _owner = owner;
        Memory = memory;
    }

    public OwnedMemorySegment Append(IMemoryOwner<byte> owner, ReadOnlyMemory<byte> memory)
    {
        var segment = new OwnedMemorySegment(owner, memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };

        Next = segment;

        return segment;
    }

    /// <inheritdoc/>
    public void Dispose() => _owner.Dispose();
}