using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core
{
    // Explicit listener ownership (#76): a listener is registered once, removed by its owner (OnDestroy), and a
    // notification iterates the snapshot taken when it started, so adding or removing during a callback is safe.
    public sealed class ListenerSet<T> where T : class
    {
        private T[] items = Array.Empty<T>();
        public IReadOnlyList<T> Items => items;
        public int Count => items.Length;
        public bool Add(T listener)
        {
            if (listener == null || Array.IndexOf(items, listener) >= 0) return false;
            var next = new T[items.Length + 1];
            Array.Copy(items, next, items.Length);
            next[items.Length] = listener;
            items = next;
            return true;
        }
        public bool Remove(T listener)
        {
            int index = listener == null ? -1 : Array.IndexOf(items, listener);
            if (index < 0) return false;
            var next = new T[items.Length - 1];
            Array.Copy(items, 0, next, 0, index);
            Array.Copy(items, index + 1, next, index, items.Length - index - 1);
            items = next;
            return true;
        }
    }
}
