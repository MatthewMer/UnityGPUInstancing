using System.Collections.Generic;

namespace Custom
{
    namespace Concurrent
    {
        public class KeyedQueue<Tkey, Tvalue>
        {
            private readonly Dictionary<Tkey, LinkedListNode<(Tkey key, Tvalue value)>> m_Map = new();
            private readonly LinkedList<(Tkey key, Tvalue value)> m_List = new();

            private readonly object m_Lock = new object();

            public void Enqueue(Tkey key, Tvalue value)
            {
                lock (m_Lock)
                {
                    if (m_Map.ContainsKey(key)) return;

                    var node = new LinkedListNode<(Tkey, Tvalue)>((key, value));
                    m_List.AddLast(node);
                    m_Map[key] = node;
                }
            }

            public bool TryDequeue(out Tkey key, out Tvalue value)
            {
                LinkedListNode<(Tkey key, Tvalue value)> node = null;

                lock (m_Lock)
                {
                    if (m_List.First != null)
                    {
                        node = m_List.First;
                        m_List.RemoveFirst();
                        m_Map.Remove(node.Value.key);

                        key = node.Value.key;
                        value = node.Value.value;
                        return true;
                    }
                    else
                    {
                        key = default;
                        value = default;
                        return false;
                    }
                }
            }

            public bool TryRemove(Tkey key, out Tvalue value)
            {
                lock (m_Lock)
                {
                    if (m_Map.TryGetValue(key, out var node))
                    {
                        m_List.Remove(node);
                        m_Map.Remove(key);

                        value = node.Value.value;
                        return true;
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                }
            }
        }
    }
}