using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

// ReSharper disable LocalizableElement

namespace ParallelDemo.ReadWriteLock
{


  public class TestRwLock
  {
    [Test]
    public void TestSimpleReadWriteLock()
    {
      var sentry = new SimpleReadWriteLock();
      var sw = new Stopwatch();
      sw.Start();

      Task.WaitAll(
        //first read
        Task.Run(delegate
        {
          using (new SimpleReadLockCookie(sentry))
          {
            Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);

            Thread.Sleep(500);

            Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);
          }
        }),

        //write
        Task.Run(async delegate
        {
          await Task.Delay(100);


          using (new SimpleWriteLockCookie(sentry))
          {
            Console.WriteLine("Acquired write: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);

            Thread.Sleep(500);
            Console.WriteLine("Releasing write: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);
          }
        }),

        //read that starts after write but executes before
        Task.Run(async delegate
        {
          await Task.Delay(200);

          using (new SimpleReadLockCookie(sentry))
          {
            using (new SimpleReadLockCookie(sentry))
            {
              Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
                Thread.CurrentThread.ManagedThreadId);

              Thread.Sleep(500);

              Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
                Thread.CurrentThread.ManagedThreadId);
            }
          }
        }),

        //this read task must wait for write on
        Task.Run(async delegate
        {
          await Task.Delay(1000);

          using (new SimpleReadLockCookie(sentry))
          {
            Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);
            Thread.Sleep(500);

            Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
              Thread.CurrentThread.ManagedThreadId);
          }
        })
        );


      sw.Stop();
      Console.WriteLine("Test finished");
    }




    [Test]
    public void TestReadWriteLockSlim()
    {
      var sentry = new ReaderWriterLockSlim();
      var sw = new Stopwatch();
      sw.Start();

      var barrier = new CountdownEvent(4);

      //first read
      ThreadPool.QueueUserWorkItem(_ =>
      {
        using (sentry.UsingReadLock())
        {
          Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);

          Thread.Sleep(500);

          Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);
        }
        barrier.Signal();
      });

      //write
      ThreadPool.QueueUserWorkItem(_ =>
      {
        Thread.Sleep(100);

        using (sentry.UsingWriteLock())
        {
          Console.WriteLine("Acquired write: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);

          Thread.Sleep(500);
          Console.WriteLine("Releasing write: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);
        }
        barrier.Signal();
      });

      //read that starts after write but executes before
      ThreadPool.QueueUserWorkItem(_ =>
      {
        Thread.Sleep(200);

        using (sentry.UsingReadLock())
        {

          Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);

          Thread.Sleep(500);

          Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);
        }
        barrier.Signal();
      });

        //this read task must wait for write on
      ThreadPool.QueueUserWorkItem(_ =>
      {
        Thread.Sleep(1000);

        using (sentry.UsingReadLock())
        {
          Console.WriteLine("Acquired read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);
          Thread.Sleep(500);

          Console.WriteLine("Releasing read: {0}ms, thread={1}", sw.ElapsedMilliseconds,
            Thread.CurrentThread.ManagedThreadId);
        }
        barrier.Signal();
      });

      barrier.Wait();   
      sw.Stop();
      Console.WriteLine("Test finished");
    }

  }
}