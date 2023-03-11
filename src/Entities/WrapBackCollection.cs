using System.Collections;

namespace Fergun;

/// <summary>
/// Provides an enumerator that wraps back to the start after reaching the last element.
/// </summary>
/// <typeparam name="T">The type of the elements to enumerate.</typeparam>
public class WrapBackCollection<T> : IReadOnlyCollection<T>
{
    private readonly T[] _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapBackCollection{T}"/> struct.
    /// </summary>
    /// <param name="items">The items.</param>
    public WrapBackCollection(T[] items)
    {
        _items = items;
        Index = 0;
    }

    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    public int Index { get; set; }

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <summary>
    /// Gets the items.
    /// </summary>
    public IList<T> Items => _items;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => new WrapBackEnumerator(_items, Index);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="WrapBackCollection{T}"/>.
    /// </summary>
    private sealed class WrapBackEnumerator : IEnumerator<T>
    {
        private readonly IReadOnlyList<T> _items;
        private int _index;
        private int _increment;

        
        internal WrapBackEnumerator(IReadOnlyList<T> items, int index)
        {
            _items = items;
            _index = index;
            _increment = 0;
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_increment >= _items.Count)
            {
                return false;
            }

            _index = _index == _items.Count - 1 ? 0 : _index + 1;
            _increment++;

            return true;
        }

        /// <inheritdoc/>
        public void Reset() => throw new NotSupportedException();

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <summary>
        /// Gets the current element.
        /// </summary>
        public T Current => _items[_index];

        /// <inheritdoc/>
        object IEnumerator.Current => Current!;
    }
}