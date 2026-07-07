using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// FIFO waiting room for frame-open requests that should fire once the stack drains. Each
    /// request carries a de-duplication key (the content type): a key already waiting is not
    /// enqueued twice. An optional capacity caps how many can wait; a value of zero (or less)
    /// means unbounded. All behaviour here is engine-free and unit-tested.
    /// </summary>
    public sealed class FrameQueueModel<T>
    {
        private readonly LinkedList<Entry> _entries = new LinkedList<Entry>();
        private readonly int _capacity;

        private readonly struct Entry
        {
            public readonly Type Key;
            public readonly T Payload;
            public Entry(Type key, T payload) { Key = key; Payload = payload; }
        }

        public FrameQueueModel(int capacity)
        {
            _capacity = capacity;
        }

        public int Count => _entries.Count;

        public int Capacity => _capacity;

        public bool IsEmpty => _entries.Count == 0;

        public bool Contains(Type key)
        {
            if (key == null) return false;
            foreach (var e in _entries)
                if (e.Key == key) return true;
            return false;
        }

        /// <summary>
        /// Adds a request to the tail. Refuses (returns false) when the key is already waiting or
        /// when a positive capacity has been reached.
        /// </summary>
        public bool Enqueue(Type key, T payload)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (Contains(key)) return false;
            if (_capacity > 0 && _entries.Count >= _capacity) return false;
            _entries.AddLast(new Entry(key, payload));
            return true;
        }

        /// <summary>Pulls the oldest waiting request. Returns false when empty.</summary>
        public bool TryDequeue(out Type key, out T payload)
        {
            var node = _entries.First;
            if (node == null)
            {
                key = null;
                payload = default;
                return false;
            }
            _entries.RemoveFirst();
            key = node.Value.Key;
            payload = node.Value.Payload;
            return true;
        }

        public void Clear() => _entries.Clear();
    }
}
