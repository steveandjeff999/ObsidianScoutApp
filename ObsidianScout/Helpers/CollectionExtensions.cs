using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace ObsidianScout.Helpers
{
 public static class CollectionExtensions
 {
 public static Task ClearOnMainThreadAsync<T>(this ObservableCollection<T> collection)
 {
 if (collection == null) return Task.CompletedTask;
 if (MainThread.IsMainThread)
 {
 try { collection.Clear(); } catch { }
 return Task.CompletedTask;
 }

 var tcs = new TaskCompletionSource<object?>();
 MainThread.BeginInvokeOnMainThread(() =>
 {
 try { collection.Clear(); tcs.SetResult(null); }
 catch (Exception ex) { tcs.SetException(ex); }
 });
 return tcs.Task;
 }

 public static Task AddOnMainThreadAsync<T>(this ObservableCollection<T> collection, T item)
 {
 if (collection == null) return Task.CompletedTask;
 if (MainThread.IsMainThread)
 {
 try { collection.Add(item); } catch { }
 return Task.CompletedTask;
 }

 var tcs = new TaskCompletionSource<object?>();
 MainThread.BeginInvokeOnMainThread(() =>
 {
 try { collection.Add(item); tcs.SetResult(null); }
 catch (Exception ex) { tcs.SetException(ex); }
 });
 return tcs.Task;
 }

 public static Task InsertOnMainThreadAsync<T>(this ObservableCollection<T> collection, int index, T item)
 {
 if (collection == null) return Task.CompletedTask;
 if (MainThread.IsMainThread)
 {
 try { collection.Insert(index, item); } catch { }
 return Task.CompletedTask;
 }

 var tcs = new TaskCompletionSource<object?>();
 MainThread.BeginInvokeOnMainThread(() =>
 {
 try { collection.Insert(index, item); tcs.SetResult(null); }
 catch (Exception ex) { tcs.SetException(ex); }
 });
 return tcs.Task;
 }
 }
}
