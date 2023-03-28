using System;

namespace Fergun;

/// <summary>
/// Represents an enumerator that slices <see cref="ReadOnlySpan{T}"/> by a separator.
/// </summary>
/// <typeparam name="T">The type of objects to enumerate.</typeparam>
public ref struct SpanSplitEnumerator<T>
    where T : IEquatable<T>
{
    private readonly T _separator;
    private ReadOnlySpan<T> _sequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanSplitEnumerator{T}"/> structure.
    /// </summary>
    /// <param name="span">The <see cref="ReadOnlySpan{T}"/> to enumerate.</param>
    /// <param name="separator">The separator.</param>
    public SpanSplitEnumerator(ReadOnlySpan<T> span, T separator)
    {
        _sequence = span;
        _separator = separator;
    }

    /// <summary>
    /// Gets the element at the current position of the enumerator.
    /// </summary>
    public ReadOnlySpan<T> Current { get; private set; } = default;

    /// <summary>
    /// Gets a value indicating whether this <see cref="SpanSplitEnumerator{T}"/> has finished enumerating.
    /// </summary>
    public bool IsFinished { get; private set; } = false;

    /// <summary>
    /// Returns the current enumerator.
    /// </summary>
    /// <returns>The current enumerator.</returns>
    public readonly SpanSplitEnumerator<T> GetEnumerator() => this;

    /// <summary>
    /// Advances the enumerator to the next element in the <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <returns>Whether there is another item in the enumerator.</returns>
    public bool MoveNext()
    {
        if (IsFinished)
        {
            return false;
        }

        do
        {
            int index = _sequence.IndexOf(_separator);
            if (index < 0)
            {
                Current = _sequence;
                IsFinished = true;
                return !Current.IsEmpty;
            }

            Current = _sequence[..index];
            _sequence = _sequence[(index + 1)..];
        }
        while (Current.IsEmpty);

        return true;
    }
}