namespace Fergun;

/// <summary>
/// Provides an enumerator that wraps back to the start after reaching the last element.
/// </summary>
/// <typeparam name="T">The type of the elements to enumerate.</typeparam>
public struct WrapBackEnumerable<T>
{
    private readonly T[] _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrapBackEnumerable{T}"/> struct.
    /// </summary>
    /// <param name="items">The items.</param>
    public WrapBackEnumerable(T[] items)
    {
        _items = items;
        Index = 0;
    }

    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Returns the enumerator.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public readonly Enumerator GetEnumerator() => new(_items,  Index);

    /// <summary>
    /// Enumerates the elements of a <see cref="WrapBackEnumerable{T}"/>.
    /// </summary>
    public struct Enumerator
    {
        private readonly IReadOnlyList<T> _items;
        private int _index;
        private int _increment;

        
        internal Enumerator(IReadOnlyList<T> items, int index)
        {
            _items = items;
            _index = index;
            _increment = 0;
        }

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
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

        /// <summary>
        /// Gets the current element.
        /// </summary>
        public readonly T Current => _items[_index];
    }
}