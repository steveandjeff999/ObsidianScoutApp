using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObsidianScout.Helpers
{
 /// <summary>
 /// Simple async-compatible lock (SemaphoreSlim wrapper) to prevent concurrent access from multiple threads.
 /// Use `using (await _lock.LockAsync()) { ... }` to acquire.
 /// </summary>
 public sealed class AsyncLock
 {
 private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1,1);

 public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
 {
 await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
 return new Releaser(_semaphore);
 }

 private sealed class Releaser : IDisposable
 {
 private SemaphoreSlim? _semaphore;

 public Releaser(SemaphoreSlim semaphore)
 {
 _semaphore = semaphore;
 }

 public void Dispose()
 {
 var s = Interlocked.Exchange(ref _semaphore, null);
 s?.Release();
 }
 }
 }
}
