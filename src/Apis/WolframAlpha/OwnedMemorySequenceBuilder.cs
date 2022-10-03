using System.Buffers;

namespace Fergun.Apis.WolframAlpha;

internal sealed class OwnedMemorySequenceBuilder<T> : IDisposable
{
    private OwnedMemorySegment<T>? _start;
    private OwnedMemorySegment<T>? _end;

    public void Append(IMemoryOwner<T> owner, Memory<T> memory)
    {
        if (_start is null)
        {
            _start = new OwnedMemorySegment<T>(owner, memory);
        }
        else if (_end is null)
        {
            _end = _start.Append(owner, memory);
        }
        else
        {
            _end = _end.Append(owner, memory);
        }
    }

    public ReadOnlySequence<T> Build()
    {
        if (_start is null) return ReadOnlySequence<T>.Empty;
        return _end is null ? new ReadOnlySequence<T>(_start.Memory) : new ReadOnlySequence<T>(_start, 0, _end, _end.Memory.Length);
    }

    public void Dispose()
    {
        var current = _start;

        while (current is not null)
        {
            current.Dispose();
            current = current.Next as OwnedMemorySegment<T>;
        }
    }
}