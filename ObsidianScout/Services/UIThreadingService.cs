using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObsidianScout.Services
{
    /// <summary>
    /// Service to ensure all long-running operations happen off the UI thread
    /// preventing UI freezes and ANR (Application Not Responding) errors
    /// </summary>
    public interface IUIThreadingService
 {
        /// <summary>
   /// Execute action on UI thread
        /// </summary>
        Task RunOnUIThreadAsync(Action action);

        /// <summary>
 /// Execute function on UI thread and return result
        /// </summary>
  Task<T> RunOnUIThreadAsync<T>(Func<T> function);

        /// <summary>
     /// Execute async action on UI thread
        /// </summary>
        Task RunOnUIThreadAsync(Func<Task> asyncAction);

      /// <summary>
        /// Execute async function on UI thread and return result
        /// </summary>
    Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> asyncFunction);

        /// <summary>
  /// Execute action on background thread
      /// </summary>
        Task RunOnBackgroundThreadAsync(Action action);

        /// <summary>
     /// Execute function on background thread and return result
        /// </summary>
 Task<T> RunOnBackgroundThreadAsync<T>(Func<T> function);

      /// <summary>
        /// Execute async action on background thread
 /// </summary>
      Task RunOnBackgroundThreadAsync(Func<Task> asyncAction);

        /// <summary>
        /// Execute async function on background thread and return result
 /// </summary>
        Task<T> RunOnBackgroundThreadAsync<T>(Func<Task<T>> asyncFunction);

/// <summary>
        /// Execute action with timeout
        /// </summary>
        Task<bool> TryRunWithTimeoutAsync(Func<Task> action, TimeSpan timeout);

      /// <summary>
        /// Check if currently on UI thread
        /// </summary>
        bool IsOnUIThread();
    }

    public class UIThreadingService : IUIThreadingService
    {
        private readonly SemaphoreSlim _uiSemaphore = new(1, 1);
        private readonly SemaphoreSlim _backgroundSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);

        public bool IsOnUIThread()
        {
      return MainThread.IsMainThread;
      }

public async Task RunOnUIThreadAsync(Action action)
        {
            if (MainThread.IsMainThread)
   {
                action();
  return;
          }

            await _uiSemaphore.WaitAsync();
         try
     {
         await MainThread.InvokeOnMainThreadAsync(action);
       }
  finally
            {
    _uiSemaphore.Release();
            }
        }

        public async Task<T> RunOnUIThreadAsync<T>(Func<T> function)
        {
       if (MainThread.IsMainThread)
     {
  return function();
            }

       await _uiSemaphore.WaitAsync();
   try
            {
            var tcs = new TaskCompletionSource<T>();
                
     await MainThread.InvokeOnMainThreadAsync(() =>
     {
       try
   {
  var result = function();
      tcs.SetResult(result);
    }
         catch (Exception ex)
         {
     tcs.SetException(ex);
 }
        });

           return await tcs.Task;
        }
   finally
   {
_uiSemaphore.Release();
            }
  }

        public async Task RunOnUIThreadAsync(Func<Task> asyncAction)
  {
 if (MainThread.IsMainThread)
            {
                await asyncAction();
     return;
       }

      await _uiSemaphore.WaitAsync();
   try
      {
          var tcs = new TaskCompletionSource();

        await MainThread.InvokeOnMainThreadAsync(async () =>
         {
      try
        {
    await asyncAction();
        tcs.SetResult();
     }
   catch (Exception ex)
               {
                tcs.SetException(ex);
           }
     });

    await tcs.Task;
         }
            finally
{
          _uiSemaphore.Release();
            }
    }

        public async Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> asyncFunction)
        {
            if (MainThread.IsMainThread)
      {
      return await asyncFunction();
    }

   await _uiSemaphore.WaitAsync();
            try
            {
     var tcs = new TaskCompletionSource<T>();

       await MainThread.InvokeOnMainThreadAsync(async () =>
       {
            try
          {
       var result = await asyncFunction();
   tcs.SetResult(result);
   }
    catch (Exception ex)
{
      tcs.SetException(ex);
        }
     });

    return await tcs.Task;
        }
            finally
    {
            _uiSemaphore.Release();
            }
        }

        public async Task RunOnBackgroundThreadAsync(Action action)
        {
     if (!MainThread.IsMainThread)
            {
     action();
return;
       }

  await _backgroundSemaphore.WaitAsync();
 try
            {
          await Task.Run(action);
          }
      finally
            {
         _backgroundSemaphore.Release();
     }
     }

public async Task<T> RunOnBackgroundThreadAsync<T>(Func<T> function)
        {
            if (!MainThread.IsMainThread)
    {
      return function();
       }

            await _backgroundSemaphore.WaitAsync();
            try
     {
      return await Task.Run(function);
            }
      finally
            {
      _backgroundSemaphore.Release();
            }
        }

        public async Task RunOnBackgroundThreadAsync(Func<Task> asyncAction)
 {
      if (!MainThread.IsMainThread)
            {
  await asyncAction();
                return;
            }

      await _backgroundSemaphore.WaitAsync();
      try
         {
    await Task.Run(asyncAction);
     }
       finally
            {
                _backgroundSemaphore.Release();
            }
        }

        public async Task<T> RunOnBackgroundThreadAsync<T>(Func<Task<T>> asyncFunction)
 {
            if (!MainThread.IsMainThread)
 {
       return await asyncFunction();
            }

            await _backgroundSemaphore.WaitAsync();
            try
      {
   return await Task.Run(asyncFunction);
            }
   finally
            {
     _backgroundSemaphore.Release();
            }
        }

     public async Task<bool> TryRunWithTimeoutAsync(Func<Task> action, TimeSpan timeout)
        {
      using var cts = new CancellationTokenSource(timeout);
     try
      {
   await action().WaitAsync(cts.Token);
   return true;
 }
            catch (OperationCanceledException)
            {
     return false;
        }
   catch
     {
  return false;
            }
        }
    }
}
