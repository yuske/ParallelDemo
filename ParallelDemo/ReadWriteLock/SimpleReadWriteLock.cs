using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelDemo.ReadWriteLock
{
  class SimpleReadWriteLock
  {
    private readonly object myLock = new object();

    private readonly ThreadLocal<int> myReaders = new ThreadLocal<int>();
    private int myReaderCount;

    private int myWriterCount;
    private Thread myWriter;

    public void AcquireRead()
    {
      lock (myLock)
      {
        if (myWriter != Thread.CurrentThread)
        {
          while (myWriter != null) Monitor.Wait(myLock);
        }
        myReaders.Value++;
        myReaderCount++;
      }
    }

    public void ReleaseRead()
    {
      lock (myLock)
      {
        if (myReaders.Value <= 0) throw new InvalidOperationException("myReaders.Value <= 0");
        Debug.Assert(myWriter == null || (myWriter == Thread.CurrentThread && myReaderCount == myReaders.Value));

        --myReaders.Value;
        if (--myReaderCount == 0 && myWriter == null) Monitor.PulseAll(myLock);
      }
    }

    public void AcquireWrite()
    {
      lock (myLock)
      {
        if (myWriter == Thread.CurrentThread)
        {
          Debug.Assert(myWriterCount > 0);
          myWriterCount ++;
        }
        else
        {
          while (myWriterCount > 0 || myReaderCount > 0) Monitor.Wait(myLock);
          Debug.Assert(myWriter == null);
          myWriter = Thread.CurrentThread;
          myWriterCount++;
        }
      }
    }

    public void ReleaseWrite()
    {
      lock (myLock)
      {
        if (myWriter != Thread.CurrentThread) throw new InvalidOperationException("myWriter != Thread.CurrentThread");
        Debug.Assert(myWriterCount > 0);
        if (--myWriterCount == 0)
        {
          myWriter = null;
          Monitor.PulseAll(myLock);
        }
      }
    }
  }


  struct SimpleReadLockCookie : IDisposable
  {
    private readonly SimpleReadWriteLock myRwLock;
    public SimpleReadLockCookie(SimpleReadWriteLock rwLock) { (myRwLock = rwLock).AcquireRead();}
    public void Dispose() { myRwLock.ReleaseRead(); }
  }

  struct SimpleWriteLockCookie : IDisposable
  {
    private readonly SimpleReadWriteLock myRwLock;
    public SimpleWriteLockCookie(SimpleReadWriteLock rwLock) { (myRwLock = rwLock).AcquireWrite();}
    public void Dispose() { myRwLock.ReleaseWrite(); }
  }
}
