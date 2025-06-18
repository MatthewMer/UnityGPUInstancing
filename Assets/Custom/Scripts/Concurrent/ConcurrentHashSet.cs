using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Custom
{
    namespace Concurrent
    {
        public class ConcurrentHashSet<T> : IEnumerable<T>
        {
            private readonly ConcurrentDictionary<T, byte> _dict = new();

            public bool TryAdd(T item) => _dict.TryAdd(item, 0);
            public bool Remove(T item) => _dict.TryRemove(item, out _);
            public bool Contains(T item) => _dict.ContainsKey(item);
            public void Clear() => _dict.Clear();

            public IEnumerator<T> GetEnumerator() => _dict.Keys.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public int Count => _dict.Count;
        }
    }
}