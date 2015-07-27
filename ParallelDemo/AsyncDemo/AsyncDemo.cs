using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
// ReSharper disable LocalizableElement

namespace ParallelDemo.AsyncDemo
{
  public class AsyncDemo
  {
    int A()
    {
      Console.WriteLine("A: {0}" , Thread.CurrentThread.ManagedThreadId);
      return 1;
    }

    Task<int> B(int p)
    {
      return Task.Run(() =>
      {
        Console.WriteLine("B: {0}", Thread.CurrentThread.ManagedThreadId);
        return p + 2;
      });
    }

//    async Task<int> B(int p)
//    {
//      return p + 2;
//    }


    int C(int p)
    {
      Console.WriteLine("C: {0}", Thread.CurrentThread.ManagedThreadId);
      return p + 3;
    }


    async Task<int> AsyncMethod()
    {   
      int a = A();
      int b = await B(a).ConfigureAwait(false);
      return C(b);
    }




    [Test]
    public void TestAsync()
    {
      Console.WriteLine("Started: {0}", Thread.CurrentThread.ManagedThreadId);
      var task = EmulatedAsyncMethod();
      Assert.AreEqual(6, task.Result);
    }









    Task<int> EmulatedAsyncMethod()
    {
      int a = A();
      return Task.Factory.StartNew(() =>
      {       
        return B(a).ContinueWith(_ => C(_.Result), TaskContinuationOptions.AttachedToParent);
      }).ContinueWith(task =>
      {
        Assert.True(task.IsCompleted);
        Assert.True(task.Result.IsCompleted);
        return task.Result.Result;
      });
    }

  }
}