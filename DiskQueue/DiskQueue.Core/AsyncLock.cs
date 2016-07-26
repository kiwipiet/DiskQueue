using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiskQueue.Core
{
    // Straight-up thieved from http://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx 
    public sealed class AsyncLock
    {
        readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        readonly Task<IDisposable> _releaser;

        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> LockAsync(CancellationToken ct = default(CancellationToken))
        {
            var wait = _semaphore.WaitAsync(ct);

            // Happy path. We synchronously acquired the lock.
            if (wait.IsCompleted && !wait.IsFaulted)
                return _releaser;

            return wait
                .ContinueWith((_, state) => (IDisposable)state,
                    _releaser.Result, ct,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private sealed class Releaser : IDisposable
        {
            readonly AsyncLock _toRelease;
            internal Releaser(AsyncLock toRelease) { _toRelease = toRelease; }
            public void Dispose() { _toRelease._semaphore.Release(); }
        }
    }
}