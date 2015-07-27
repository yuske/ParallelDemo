using System;
using System.Threading;

namespace ParallelDemo.ReadWriteLock
{
  public struct ReadLockSlimCookie : IDisposable
  {
    private readonly ReaderWriterLockSlim myRwlock;

    internal ReadLockSlimCookie(ReaderWriterLockSlim rwlock)
    {
      rwlock.EnterReadLock();
      myRwlock = rwlock;
    }

    public void Dispose()
    {
      if (myRwlock != null) myRwlock.ExitReadLock();
    }
  }


  public struct WriteLockSlimCookie : IDisposable
  {
    private readonly ReaderWriterLockSlim myRwlock;

    internal WriteLockSlimCookie(ReaderWriterLockSlim rwlock)
    {
      rwlock.EnterWriteLock();
      myRwlock = rwlock;
    }

    public void Dispose()
    {
      if (myRwlock != null) myRwlock.ExitWriteLock();
    }
  }




  public static class ReadWriteLockSlimEx
  {
    public static ReadLockSlimCookie UsingReadLock(this ReaderWriterLockSlim slim)
    {
      return new ReadLockSlimCookie(slim);
    }

    public static WriteLockSlimCookie UsingWriteLock(this ReaderWriterLockSlim slim)
    {
      return new WriteLockSlimCookie(slim);
    }
  }
}