using System;
using System.Diagnostics;
using System.Threading;

namespace ParallelDemo.BlockingLazy
{
  public class SimpleBlockingLazy<T> where T:class
  {
    private readonly object myLock  = new object();
    private readonly Func<T> myProducer;

    private T myValue;

    public SimpleBlockingLazy(Func<T> producer)
    {
      myProducer = producer;
    }

    public T Value
    {
      get
      {
        var res = myValue;
        if (res != null) return res;

        lock (myLock)
        {
          if (myValue != null) return myValue;
          
          res = myProducer();
          Thread.MemoryBarrier();
          myValue = res;

          Debug.Assert(myValue != null);
          return myValue;
        }
      }
    }
  }
}