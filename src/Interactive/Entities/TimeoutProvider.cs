using System;
using System.Threading.Tasks;
using System.Threading;

namespace Fergun.Interactive
{
    internal sealed class TimeoutProvider<T>
    {
        public TimeSpan Delay { get; }

        public bool CanReset { get; }

        private bool _disposed;
        private readonly Timer _timer;
        private readonly TaskCompletionSource<T> _timeoutSource;

        public TimeoutProvider(TimeSpan delay)
        {
            Delay = delay;
            _timeoutSource = new TaskCompletionSource<T>();
            _timer = new Timer(OnTimerFired, null, delay, Timeout.InfiniteTimeSpan);
        }

        public TimeoutProvider(TimeSpan delay, bool canReset)
            : this(delay)
        {
            CanReset = canReset;
        }

        private void OnTimerFired(object state)
        {
            _disposed = true;
            _timer.Dispose();
            _timeoutSource.SetResult(default);
        }

        public bool TryReset()
        {
            if (_disposed || !CanReset)
            {
                return false;
            }

            _timer.Change(Delay, Timeout.InfiniteTimeSpan);
            return true;
        }

        public bool TryDispose()
        {
            if (_disposed)
            {
                return false;
            }

            _disposed = true;
            _timer.Dispose();
            _timeoutSource.TrySetCanceled();
            return true;
        }

        public Task WaitAsync()
            => _timeoutSource.Task;
    }
}