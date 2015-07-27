using System;
using System.Linq;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable CoVariantArrayConversion

namespace ParallelDemo
{
  public partial class MainWindow
  {
    public MainWindow()
    {
      InitializeComponent();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Return)
      {
        Do();
      }
      else if (e.Key == Key.Escape)
      {
        mySource.Cancel();
      }
    }

    private CancellationTokenSource mySource = new CancellationTokenSource();

    private void Do()
    {
      mySource = new CancellationTokenSource();

      const int ntasks = 4;
      long n;
      if (long.TryParse(N.Text, out n) && n >= 0)
      {
        var tasks = new Task<int>[ntasks];

        for (var i = 0; i < ntasks; i++)
        {
          tasks[i] = CreateTask(i*n/ntasks, (i + 1)*n/ntasks, mySource.Token);
          var currentContext = SynchronizationContext.Current;
          var currentScheduler = TaskScheduler.Current;

          tasks[i].Start();
          //tasks[i].RunSynchronously();
          //tasks[i].Start(TaskScheduler.FromCurrentSynchronizationContext());
        }


        Task.Factory.ContinueWhenAll(tasks, _ =>
        {
          try
          {
            //Task.WaitAll(tasks, mySource.Token);
            //WaitAndPump(tasks);

            var res = tasks.Aggregate(0, (acc, task) => acc + task.Result);
            Answer.Text = res.ToString();
          }
          catch (AggregateException e)
          {
            Answer.Text = e.Flatten().InnerExceptions[0].Message;
          }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

      }
      else
      {
        Answer.Text = "Invalid number: " + N.Text;
      }
    }

    private static Task<int> CreateTask(long start, long end, CancellationToken token)
    {
      return new Task<int>(() =>
      {
        Console.WriteLine(Thread.CurrentThread.ManagedThreadId);

        var res = 0;
        for (var x = Math.Max(start + 1, 2); x <= end;)
        {
          for (var j = 2; j*j <= x; j++)
            if (x%j == 0) goto outer;

          res++;

          outer:
          x++;

          token.ThrowIfCancellationRequested();
        }


        return res;
      }, token);
    }


















    public void WaitAndPump(Task[] tasks)
    {
      while (true)
      {
        if (tasks.All(t => t.IsCompleted)) break;

        DoEvents();
      }
    }


    [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
    private void DoEvents()
    {
      DispatcherFrame frame = new DispatcherFrame();
      Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
        new DispatcherOperationCallback(ExitFrame), frame);
      Dispatcher.PushFrame(frame);
    }

    private object ExitFrame(object f)
    {
      ((DispatcherFrame) f).Continue = false;

      return null;
    }
  }
}