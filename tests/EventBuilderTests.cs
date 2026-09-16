using System.Collections.Generic;
using System.Linq;
using StreamChatAI.LiveSplit.Core;
using Xunit;

namespace StreamChatAI.LiveSplit.Tests
{
    public class EventBuilderTests
    {
        /// <summary>Celeste, three splits, with a personal best of 27:40 and known best segments.</summary>
        private static RunSnapshot Run(params long?[] splits)
        {
            var pb = new long?[] { 400_000, 900_000, 1_660_000 };
            var best = new long?[] { 395_000, 490_000, 750_000 };
            var names = new[] { "Forsaken City", "-Old Site", "{Chapters}Summit" };

            return new RunSnapshot
            {
                Game = "Celeste",
                Category = "Any%",
                Attempt = 1432,
                TimingMethod = "RealTime",
                Segments = Enumerable.Range(0, 3).Select(i => new SegmentSnapshot
                {
                    Name = names[i],
                    SplitMs = i < splits.Length ? splits[i] : null,
                    PbSplitMs = pb[i],
                    BestSegmentMs = best[i],
                }).ToList(),
            };
        }

        private static Dictionary<string, object> Part(Dictionary<string, object> payload, string key) =>
            (Dictionary<string, object>)payload[key];

        [Fact]
        public void A_split_ahead_of_the_pb_has_a_negative_delta()
        {
            var payload = EventBuilder.Split(Run(388_000), "run", 0);

            Assert.Equal("split", payload["event"]);
            Assert.Equal(-12_000L, Part(payload, "split")["delta_ms"]);
            Assert.Equal(1, Part(payload, "current")["index"]);
            Assert.Equal("Old Site", Part(payload, "current")["name"]);
        }

        [Fact]
        public void A_segment_faster_than_the_best_is_gold()
        {
            // 388.0s against a best of 395.0s.
            Assert.True((bool)Part(EventBuilder.Split(Run(388_000), "run", 0), "split")["gold"]);
            // 500.0s against a best of 490.0s is not, even though the split is fine.
            Assert.False((bool)Part(EventBuilder.Split(Run(388_000, 888_000), "run", 1), "split")["gold"]);
        }

        // After a skip the time covers two segments. Calling that a gold would
        // tell chat about a record that was never set.
        [Fact]
        public void A_segment_after_a_skip_is_never_gold()
        {
            var run = Run(null, 700_000);

            Assert.Null(EventBuilder.SegmentMs(run, 1));
            Assert.False(EventBuilder.IsGold(run, 1));
        }

        [Fact]
        public void The_first_time_through_nothing_is_gold()
        {
            var run = Run(100);
            run.Segments[0].BestSegmentMs = null;

            Assert.False(EventBuilder.IsGold(run, 0));
        }

        [Fact]
        public void The_last_split_is_a_finish_and_knows_whether_it_is_a_pb()
        {
            var faster = EventBuilder.Split(Run(390_000, 880_000, 1_650_000), "run", 2);
            Assert.Equal("finish", faster["event"]);
            Assert.True((bool)Part(faster, "finish")["is_pb"]);
            Assert.Equal(1_660_000L, Part(faster, "finish")["previous_pb_ms"]);

            var slower = EventBuilder.Split(Run(390_000, 880_000, 1_670_000), "run", 2);
            Assert.False((bool)Part(slower, "finish")["is_pb"]);
        }

        [Fact]
        public void A_first_ever_finish_is_a_pb()
        {
            var run = Run(390_000, 880_000, 1_900_000);
            foreach (var segment in run.Segments)
            {
                segment.PbSplitMs = null;
            }

            Assert.True((bool)Part(EventBuilder.Split(run, "run", 2), "finish")["is_pb"]);
        }

        [Fact]
        public void Subsplit_markers_are_stripped_from_names()
        {
            var run = Run();

            Assert.Equal("Old Site", EventBuilder.Name(run, 1));
            Assert.Equal("Summit", EventBuilder.Name(run, 2));
        }

        [Fact]
        public void A_reset_carries_where_the_run_was_and_what_phase_it_was_in()
        {
            var payload = EventBuilder.Reset(Run(), "run", "Running", 1, 512_000);

            Assert.Equal("reset", payload["event"]);
            Assert.Equal(1, Part(payload, "current")["index"]);
            Assert.Equal("Old Site", Part(payload, "current")["name"]);
            Assert.Equal("Running", Part(payload, "reset")["from_phase"]);
            Assert.Equal(512_000L, Part(payload, "reset")["run_time_ms"]);
        }

        [Fact]
        public void Every_event_gets_its_own_id_so_retries_can_be_recognised()
        {
            var a = EventBuilder.Pause(Run(), "run");
            var b = EventBuilder.Pause(Run(), "run");

            Assert.NotEqual(a["event_id"], b["event_id"]);
        }

        [Fact]
        public void Sum_of_best_needs_every_segment()
        {
            Assert.Equal(1_635_000L, EventBuilder.SumOfBestMs(Run()));

            var run = Run();
            run.Segments[1].BestSegmentMs = null;
            Assert.Null(EventBuilder.SumOfBestMs(run));
        }
    }
}
