using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamChatAI.LiveSplit.Core;
using Xunit;

namespace StreamChatAI.LiveSplit.Tests
{
    public class EventSenderTests
    {
        private static readonly Func<TimeSpan, CancellationToken, Task> NoWait = (span, cancel) => Task.CompletedTask;

        [Fact]
        public async Task Sends_in_order_and_retries_a_failure_before_moving_on()
        {
            var sent = new ConcurrentQueue<string>();
            var failuresLeft = 2;

            using (var sender = new EventSender((token, json, cancel) =>
            {
                if (json == "split" && Interlocked.Decrement(ref failuresLeft) >= 0)
                {
                    return Task.FromResult(SendResult.Failed);
                }
                sent.Enqueue(json);
                return Task.FromResult(SendResult.Sent);
            }, () => "token", NoWait))
            {
                sender.Enqueue("start");
                sender.Enqueue("split");
                sender.Enqueue("finish");
                await sender.DrainAsync(TimeSpan.FromSeconds(5));
            }

            Assert.Equal(new[] { "start", "split", "finish" }, sent.ToArray());
        }

        [Fact]
        public async Task A_rejected_event_is_not_retried()
        {
            var attempts = 0;
            using (var sender = new EventSender((token, json, cancel) =>
            {
                Interlocked.Increment(ref attempts);
                return Task.FromResult(SendResult.Rejected);
            }, () => "token", NoWait))
            {
                sender.Enqueue("bad");
                await sender.DrainAsync(TimeSpan.FromSeconds(5));
            }

            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task A_revoked_token_is_reported_once()
        {
            var revoked = 0;
            using (var sender = new EventSender((token, json, cancel) => Task.FromResult(SendResult.Unauthorized), () => "token", NoWait))
            {
                sender.Unauthorized += () => Interlocked.Increment(ref revoked);
                sender.Enqueue("start");
                await sender.DrainAsync(TimeSpan.FromSeconds(5));
            }

            Assert.Equal(1, revoked);
        }

        [Fact]
        public async Task Nothing_is_queued_while_not_connected()
        {
            var attempts = 0;
            using (var sender = new EventSender((token, json, cancel) =>
            {
                Interlocked.Increment(ref attempts);
                return Task.FromResult(SendResult.Sent);
            }, () => null, NoWait))
            {
                sender.Enqueue("start");
                await sender.DrainAsync(TimeSpan.FromSeconds(1));
                Assert.Equal(0, sender.Pending);
            }

            Assert.Equal(0, attempts);
        }

        [Fact]
        public async Task A_long_outage_drops_the_oldest_events_rather_than_growing_forever()
        {
            var gate = new TaskCompletionSource<bool>();
            var sent = new List<string>();

            using (var sender = new EventSender(async (token, json, cancel) =>
            {
                await gate.Task;
                lock (sent) sent.Add(json);
                return SendResult.Sent;
            }, () => "token", NoWait))
            {
                for (var i = 0; i < EventSender.MaxQueued + 50; i++)
                {
                    sender.Enqueue(i.ToString());
                }

                Assert.True(sender.Pending <= EventSender.MaxQueued);
                gate.SetResult(true);
                await sender.DrainAsync(TimeSpan.FromSeconds(10));
            }

            lock (sent)
            {
                Assert.Contains((EventSender.MaxQueued + 49).ToString(), sent);
            }
        }
    }
}
