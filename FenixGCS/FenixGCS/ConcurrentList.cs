using System;
using System.Collections.Generic;
using System.Threading;

namespace FenixGCSApi
{
    public class ConcurrentList<T> : List<T>
    {
        private readonly object _lock = new object();
        public new void Add(T item)
        {
            lock (_lock)
            {
                base.Add(item);
            }
        }
        public new bool Remove(T item)
        {
            lock (_lock)
            {
                return base.Remove(item);
            }
        }
        public new T this[int index]
        {
            get
            {
                lock (_lock)
                {
                    return base[index];
                }
            }
            set
            {
                lock (_lock)
                {
                    base[index] = value;
                }
            }
        }
        public new int Count
        {
            get
            {
                lock (_lock)
                {
                    return base.Count;
                }
            }
        }
    }
}