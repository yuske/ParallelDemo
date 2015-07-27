using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ParallelDemo.BlockingQueue
{
  /// <summary>
  /// Concurrent blocking queue with predefined size. All operations are thread safe. Operations like <code>ToArray</code> or <code>GetEnumerator</code>
  /// takes queue snapshot at some moment.
  /// </summary>
  /// <typeparam name="T"></typeparam>
 
  public class SimpleBlockingQueue<T> : IProducerConsumerCollection<T> where T : class
  {
    private const long BlockingOperationInProgressMarker = -1;

    private readonly T[] myQueue;
    private readonly long myMask;
    private readonly int mySize;

    private long myHead;
    private long myTail;

    /// <summary>
    /// Constructs queue with predefined size. Queue size is defined logariphmically. Queue is not growing, nor shrinking.
    /// </summary>
    /// <param name="logMaxSize">From 1 to 20. Real queue size will be 1&lt;&lt;<see cref="logMaxSize"/>.</param>
    public SimpleBlockingQueue(int logMaxSize)
    {
      if (logMaxSize <= 1 || logMaxSize > 20) throw new ArgumentException("Bad logsize:" + logMaxSize);

      mySize = 1 << logMaxSize;
      myQueue = new T[mySize];
      myMask = mySize - 1;
    }


    #region Nonblocking API

    /// <summary>
    /// Enqueues to the tail of the queue. Increases tail.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>    
    public bool TryAdd(T t)
    {
      if (t == null) throw new ArgumentNullException("t");

      while (true)
      {
        //prologue
        var head = Interlocked.Read(ref myHead);
        var tail = Interlocked.Read(ref myTail);
//        if (head == BlockingOperationInProgressMarker || tail == BlockingOperationInProgressMarker)
//        {
//          Thread.Yield();
//          continue;
//        }

        //check
        if (tail - head >= mySize) return false;

        //set
        if (tail == Interlocked.CompareExchange(ref myTail, tail + 1, tail))
        {
          var idx = tail & myMask;
          myQueue[idx] = t;
          return true;
        }

        Thread.Yield();
      }
    }

    /// <summary>
    /// Dequeues from the head of the queue. Moves head.
    /// </summary>
    /// <param name="res"></param>
    /// <returns></returns>
    public bool TryDequeue(out T res)
    {
      while (true)
      {
        //prologue
        var head = Interlocked.Read(ref myHead);
        var tail = Interlocked.Read(ref myTail);
        if (head == BlockingOperationInProgressMarker || tail == BlockingOperationInProgressMarker)
        {
          Thread.Yield();
          continue;
        }

        //check
        if (head >= tail)
        {
          res = null;
          return false;
        }

        //set
        var queue = myQueue;
        var idx = head & myMask;
        res = queue[idx];
        if (res != null && head == Interlocked.CompareExchange(ref myHead, head + 1, head))
        {
          Interlocked.CompareExchange(ref queue[idx], null, res); //if some producer already set this index with new value, don't clear;
          return true;
        }

        Thread.Yield();
      }
    }

    /// <summary>
    /// Peeks from the head of the queue. Leaves head untouched. Sequential peeks will return same result if no other thread dequeues or clears simultaneously.
    /// </summary>
    /// <param name="res"></param>
    /// <returns></returns>
    public bool TryPeek(out T res)
    {
      while (true)
      {
        //prologue
        var head = Interlocked.Read(ref myHead);
        var tail = Interlocked.Read(ref myTail);
        if (head == BlockingOperationInProgressMarker || tail == BlockingOperationInProgressMarker)
        {
          Thread.Yield();
          continue;
        }

        //check
        if (head >= tail)
        {
          res = null;
          return false;
        }

        //op
        var queue = myQueue;
        var idx = head & myMask;
        if ((res = queue[idx]) != null) return true;

        Thread.Yield();
      }
    }


    /// <summary>
    /// Number of elements in queue snapshot.
    /// </summary>
    public int Count
    {
      get
      {
        while (true)
        {
          //prologue
          var head = Interlocked.Read(ref myHead);
          var tail = Interlocked.Read(ref myTail);
          if (head != BlockingOperationInProgressMarker && tail != BlockingOperationInProgressMarker)
          {
            return (int)(tail - head);
          }
          Thread.Yield();
        }
      }
    }

    #endregion



    #region Blocking API

    public void CopyTo(T[] array, int index)
    {
      DoImmutableBlockingOp((head, tail) =>
      {
        while (head < tail) array[index++] = myQueue[head++ & myMask];
      });
    }


    public T[] ToArray()
    {
      return DoImmutableBlockingOp((head, tail) =>
      {
        var index = 0;
        var array = new T[tail - head];
        while (head < tail) array[index++] = myQueue[head++ & myMask];
        return array;
      });
    }


    public void CopyTo(Array array, int index)
    {
      DoImmutableBlockingOp((head, tail) =>
      {
        while (head < tail) array.SetValue(myQueue[head++ & myMask], index++);
      });
    }

    public void Clear()
    {
      {
        while (true)
        {
          //prologue
          var head = Interlocked.Read(ref myHead);
          var tail = Interlocked.Read(ref myTail);
          if (head == BlockingOperationInProgressMarker || tail == BlockingOperationInProgressMarker)
          {
            Thread.Yield();
            continue;
          }

          //set
          if (head == Interlocked.CompareExchange(ref myHead, BlockingOperationInProgressMarker, head))
          {
            if (tail == Interlocked.CompareExchange(ref myTail, BlockingOperationInProgressMarker, tail))
            {
              try
              {
                Array.Clear(myQueue, 0, mySize);
              }
              finally
              {
                var prevtail = Interlocked.Exchange(ref myTail, 0);
                var prevhead = Interlocked.Exchange(ref myHead, 0);
                Debug.Assert(prevtail == BlockingOperationInProgressMarker, "Invalid state: tail");
                Debug.Assert(prevhead == BlockingOperationInProgressMarker, "Invalid state: head");
              }
              return;
            }
            else
            {
              var prevhead = Interlocked.Exchange(ref myHead, head);
              Debug.Assert(prevhead == BlockingOperationInProgressMarker, "Invalid state: head");
            }
          }


          //continue
          Thread.Yield();
        }
      }
    }


    private void DoImmutableBlockingOp(Action<long, long> op)
    {
      DoImmutableBlockingOp<object>((head, tail) => { op(head, tail); return null; });
    }

    private TParam DoImmutableBlockingOp<TParam>(Func<long, long, TParam> op)
    {
      while (true)
      {
        //prologue
        var head = Interlocked.Read(ref myHead);
        var tail = Interlocked.Read(ref myTail);
        if (head == BlockingOperationInProgressMarker || tail == BlockingOperationInProgressMarker)
        {
          Thread.Yield();
          continue;
        }

        //set
        if (head == Interlocked.CompareExchange(ref myHead, BlockingOperationInProgressMarker, head))
        {
          try
          {
            if (tail == Interlocked.CompareExchange(ref myTail, BlockingOperationInProgressMarker, tail))
            {
              try
              {
                return op(head, tail);
              }
              finally
              {
                var prevtail = Interlocked.Exchange(ref myTail, tail);
                Debug.Assert(prevtail == BlockingOperationInProgressMarker, "Invalid state: tail");
              }

            }
          }
          finally
          {
            var prevhead = Interlocked.Exchange(ref myHead, head);
            Debug.Assert(prevhead == BlockingOperationInProgressMarker, "Invalid state: head");
          }
        }

        //continue
        Thread.Yield();
      }
    }


    #endregion



    #region Other Collection API

    /// <summary>
    /// Not real SyncRoot, but you can use it for external synchronization
    /// </summary>
    public object SyncRoot
    {
      get { return myQueue; }
    }


    public bool IsSynchronized
    {
      get { return true; }
    }

    public bool IsEmpty
    {
      get { return Count == 0; }
    }

    public IEnumerator<T> GetEnumerator()
    {
      return ((IList<T>)ToArray()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// Same as <see cref="TryDequeue"/>. Implementation of <see cref="IProducerConsumerCollection{T}.TryTake"/>
    /// </summary>
    /// <param name="res"></param>
    /// <returns></returns>
    public bool TryTake(out T res)
    {
      return TryDequeue(out res);
    }
    #endregion


    #region Simple API

    /// <summary>
    /// Same as <see cref="TryDequeue"/> but returns <code>null</code> <see cref="TryDequeue"/> returns false
    /// </summary>
    /// <returns></returns>
    public T Dequeue()
    {
      T res;
      TryDequeue(out res);
      return res;
    }

    /// <summary>
    /// Same as <see cref="TryPeek"/> but returns <code>null</code> when <see cref="TryPeek"/> returns false
    /// </summary>
    /// <returns></returns>
    public T Peek()
    {
      T res;
      TryPeek(out res);
      return res;
    }

    #endregion
  }
}