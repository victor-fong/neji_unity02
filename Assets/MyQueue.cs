using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace Neji {

    public class MyQueue<T>
    {

        // private readonly PriorityQueue<TElement,TPriority> queue = new PriorityQueue<TElement,TPriority>();
        private readonly LinkedList<T> list = new LinkedList<T>();

        private int maxSize;

        public MyQueue(int maxSize)
        {
            this.maxSize = maxSize;
        }

        public int Size()
        {
            return 0;
        }

        public void Enqueue(T item)
        {
            lock (list)
            {
                list.AddLast(item);
                if (list.Count > maxSize)
                {
                    list.RemoveFirst();
                }
                // wake up any blocked dequeue
                Monitor.Pulse(list);
            }
        }

        public T Dequeue()
        {
            lock (list)
            {
                while (list.Count == 0)
                {
                    Monitor.Wait(list);
                }

                T result = list.First.Value;
                list.RemoveFirst();
                return result;
            }
        }
    }
}
