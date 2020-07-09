using System.Collections.Generic;

namespace Discord.Addons.CommandCache
{
    public interface ICommandCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }
        int Count { get; }

        void Add(TKey key, TValue value);
        void Add(IUserMessage command, IUserMessage response);
        void Clear();
        bool ContainsKey(TKey key);
        bool Remove(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }
}