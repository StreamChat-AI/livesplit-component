using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StreamChatAI.LiveSplit.Core
{
    /// <summary>
    /// Sends events one at a time, in order, off LiveSplit's UI thread.
    ///
    /// ⚠️ Never block the timer. Every LiveSplit event handler runs on the
    /// thread that draws the timer, so a slow network there would make the
    /// timer itself stutter mid-run.
    ///
    /// Order matters more than speed: a split arriving before its start would
    /// be recorded against the wrong attempt. So a failed event is retried
    /// before anything behind it is sent, and given up on after a while rather
    /// than holding every later event hostage.
    /// </summary>
    public sealed class EventSender : IDisposable
    {
        public const int MaxQueued = 500;

        private static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20),
        };

        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly Func<string, string, CancellationToken, Task<SendResult>> _send;
        private readonly Func<string> _token;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly Task _worker;

        /// <summary>Raised on the sender's thread when the server says the token is dead.</summary>
        public event Action Unauthorized;

        public EventSender(
            Func<string, string, CancellationToken, Task<SendResult>> send,
            Func<string> token,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _send = send;
            _token = token;
            _delay = delay ?? ((span, cancel) => Task.Delay(span, cancel));
            _worker = Task.Run(RunAsync);
        }

        public int Pending => _queue.Count;

        public void Enqueue(string json)
        {
            if (_token() == null)
            {
                return; // not connected: nothing to send it with
            }

            // A long outage should cost old events, not memory.
            while (_queue.Count >= MaxQueued && _queue.TryTake(out _))
            {
            }

            _queue.TryAdd(json);
        }

        private async Task RunAsync()
        {
            try
            {
                foreach (var json in _queue.GetConsumingEnumerable(_stop.Token))
                {
                    await DeliverAsync(json).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task DeliverAsync(string json)
        {
            for (var attempt = 0; attempt <= Backoff.Length; attempt++)
            {
                var token = _token();
                if (token == null)
                {
                    return;
                }

                SendResult result;
                try
                {
                    result = await _send(token, json, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    result = SendResult.Failed;
                }

                switch (result)
                {
                    case SendResult.Sent:
                    case SendResult.Rejected:
                        return;
                    case SendResult.Unauthorized:
                        Unauthorized?.Invoke();
                        return;
                }

                if (attempt < Backoff.Length)
                {
                    // Every event carries its own id, so a retry that the
                    // server had in fact received is recognised and ignored.
                    await _delay(Backoff[attempt], _stop.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>For tests: waits until everything queued has been attempted.</summary>
        public async Task DrainAsync(TimeSpan timeout)
        {
            var until = DateTime.UtcNow + timeout;
            while (_queue.Count > 0 && DateTime.UtcNow < until)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
            await Task.Delay(50).ConfigureAwait(false);
        }

        /// <summary>
        /// Gives what is queued a moment to go before stopping. LiveSplit
        /// closing mid-run resets the timer and disposes the component in the
        /// same breath, and without this the reset is the event lost - leaving
        /// chat told the run is still going.
        /// </summary>
        public void Dispose() => Dispose(TimeSpan.FromSeconds(2));

        public void Dispose(TimeSpan drain)
        {
            _queue.CompleteAdding();
            try
            {
                if (!_worker.Wait(drain))
                {
                    _stop.Cancel();
                    _worker.Wait(TimeSpan.FromSeconds(1));
                }
            }
            catch (AggregateException)
            {
            }
            _stop.Cancel();
            _stop.Dispose();
        }
    }
}
