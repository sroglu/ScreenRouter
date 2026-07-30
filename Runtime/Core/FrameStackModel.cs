using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Ordered container for the live modal stack, kept engine-free so its ordering behaviour is
    /// unit-testable. Entries are held oldest-first (index 0 = bottom, last = top). A doubly
    /// linked list backs it so arbitrary removals (closing a frame that is not on top) stay cheap.
    /// </summary>
    public sealed class FrameStackModel<T> where T : class
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();

        public int Count => _items.Count;

        public bool IsEmpty => _items.Count == 0;

        /// <summary>The frame currently on top, or null when empty.</summary>
        public T Top => _items.Last?.Value;

        /// <summary>The frame at the bottom of the stack, or null when empty.</summary>
        public T Bottom => _items.First?.Value;

        public void PushTop(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _items.AddLast(item);
        }

        /// <summary>Removes and returns the top frame, or null when empty.</summary>
        public T PopTop()
        {
            var node = _items.Last;
            if (node == null) return null;
            _items.RemoveLast();
            return node.Value;
        }

        /// <summary>Removes a specific frame wherever it sits. Returns false if absent.</summary>
        public bool Remove(T item) => item != null && _items.Remove(item);

        public bool Contains(T item) => item != null && _items.Contains(item);

        /// <summary>Walks from the top down and returns the first match, or null.</summary>
        public T FindTopDown(Predicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            for (var node = _items.Last; node != null; node = node.Previous)
            {
                if (match(node.Value)) return node.Value;
            }
            return null;
        }

        /// <summary>Enumerates top-to-bottom (topmost frame first).</summary>
        public IEnumerable<T> TopToBottom()
        {
            for (var node = _items.Last; node != null; node = node.Previous)
                yield return node.Value;
        }

        /// <summary>Enumerates bottom-to-top; the index of each item is its sorting depth.</summary>
        public IEnumerable<T> BottomToTop()
        {
            for (var node = _items.First; node != null; node = node.Next)
                yield return node.Value;
        }

        public void Clear() => _items.Clear();
    }
}
