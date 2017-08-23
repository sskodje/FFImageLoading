﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace FFImageLoading.Concurrency
{
    /// <summary>
    /// A simplified priority queue implementation.  Is stable, auto-resizes, and thread-safe, at the cost of being slightly slower than
    /// FastPriorityQueue
    /// </summary>
    /// <typeparam name="TItem">The type to enqueue</typeparam>
    /// <typeparam name="TPriority">The priority-type to use for nodes.  Must extend IComparable&lt;TPriority&gt;</typeparam>
    public class SimplePriorityQueue<TItem, TPriority> : IPriorityQueue<TItem, TPriority>
        where TPriority : IComparable<TPriority>
    {        
        protected readonly IFixedSizePriorityQueue<SimpleNode<TItem, TPriority>, TPriority> _queue;

        public SimplePriorityQueue(IFixedSizePriorityQueue<SimpleNode<TItem, TPriority>, TPriority> queue)
        {
            _queue = queue;
        }

        /// <summary>
        /// Returns the number of nodes in the queue.
        /// O(1)
        /// </summary>
        public int Count
        {
            get
            {
                lock (_queue)
                {
                    return _queue.Count;
                }
            }
        }


        /// <summary>
        /// Returns the head of the queue, without removing it (use Dequeue() for that).
        /// Throws an exception when the queue is empty.
        /// O(1)
        /// </summary>
        public TItem First
        {
            get
            {
                lock (_queue)
                {
                    if (_queue.Count <= 0)
                    {
                        throw new InvalidOperationException("Cannot call .First on an empty queue");
                    }

                    var first = _queue.First;
                    return (first != null ? first.Data : default(TItem));
                }
            }
        }

        /// <summary>
        /// Removes every node from the queue.
        /// O(n)
        /// </summary>
        public void Clear()
        {
            lock (_queue)
            {
                _queue.Clear();
            }
        }

        /// <summary>
        /// Returns whether the given item is in the queue.
        /// O(n)
        /// </summary>
        public bool Contains(TItem item)
        {
            lock (_queue)
            {
                var comparer = EqualityComparer<TItem>.Default;
                foreach (var node in _queue)
                {
                    if (comparer.Equals(node.Data, item))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Removes the head of the queue (node with maximum priority; ties are broken by order of insertion), and returns it.
        /// If queue is empty, throws an exception
        /// O(log n)
        /// </summary>
        public virtual TItem Dequeue()
        {
            lock (_queue)
            {
                if (_queue.Count <= 0)
                {
                    throw new InvalidOperationException("Cannot call Dequeue() on an empty queue");
                }

                var node = _queue.Dequeue();
                return node.Data;
            }
        }

        /// <summary>
        /// Tries to remove the head of the queue
        /// </summary>
        /// <param name="item"><see cref="TItem"/></param>
        /// <returns>If queue is empty <value>false</value>, else <value>true</value></returns>
        public virtual bool TryDequeue(out TItem item)
        {
            lock (_queue)
            {
                if (_queue.Count < 1)
                {
                    item = default(TItem);
                    return false;
                }

                try
                {
                    var node = _queue.Dequeue();
                    item = node.Data;
                    return true;
                }
                catch (InvalidOperationException)
                {
                    item = default(TItem);
                    return false;
                }
            }
        }

        /// <summary>
        /// Enqueue a node to the priority queue.  Higher values are placed in front. Ties are broken by first-in-first-out.
        /// This queue automatically resizes itself, so there's no concern of the queue becoming 'full'.
        /// Duplicates are allowed.
        /// O(log n)
        /// </summary>
        public virtual void Enqueue(TItem item, TPriority priority)
        {
            lock (_queue)
            {
                var node = new SimpleNode<TItem, TPriority>(item);
                if (_queue.Count == _queue.MaxSize)
                {
                    _queue.Resize(_queue.MaxSize * 2 + 1);
                }
                _queue.Enqueue(node, priority);
            }
        }

        /// <summary>
        /// Removes an item from the queue.  The item does not need to be the head of the queue.  
        /// If the item is not in the queue, an exception is thrown.  If unsure, check Contains() first.
        /// If multiple copies of the item are enqueued, only the first one is removed. 
        /// O(n)
        /// </summary>
        public virtual void Remove(TItem item)
        {
            lock (_queue)
            {
                try
                {
                    _queue.Remove(GetExistingNode(item));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException("Cannot call Remove() on a node which is not enqueued: " + item, ex);
                }
            }
        }

        /// <summary>
        /// Call this method to change the priority of an item.
        /// Calling this method on a item not in the queue will throw an exception.
        /// If the item is enqueued multiple times, only the first one will be updated.
        /// (If your requirements are complex enough that you need to enqueue the same item multiple times <i>and</i> be able
        /// to update all of them, please wrap your items in a wrapper class so they can be distinguished).
        /// O(n)
        /// </summary>
        public void UpdatePriority(TItem item, TPriority priority)
        {
            lock (_queue)
            {
                try
                {
                    var updateMe = GetExistingNode(item);
                    _queue.UpdatePriority(updateMe, priority);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException("Cannot call UpdatePriority() on a node which is not enqueued: " + item, ex);
                }
            }
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            List<TItem> queueData = new List<TItem>();
            lock (_queue)
            {
                //Copy to a separate list because we don't want to 'yield return' inside a lock
                foreach (var node in _queue)
                {
                    queueData.Add(node.Data);
                }
            }

            return queueData.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

#if DEBUG
        public bool IsValidQueue()
        {
            lock (_queue)
            {
                return _queue.IsValidQueue();
            }
        }
#endif

        /// <summary>
        /// Given an item of type T, returns the exist SimpleNode in the queue
        /// </summary>
        private SimpleNode<TItem, TPriority> GetExistingNode(TItem item)
        {
            var comparer = EqualityComparer<TItem>.Default;
            foreach (var node in _queue)
            {
                if (comparer.Equals(node.Data, item))
                {
                    return node;
                }
            }
            throw new InvalidOperationException("Item cannot be found in queue: " + item);
        }

    }
}
