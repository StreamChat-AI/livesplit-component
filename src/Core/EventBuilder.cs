using System;
using System.Collections.Generic;

namespace StreamChatAI.LiveSplit.Core
{
    /// <summary>
    /// Turns LiveSplit's timer events into the payloads the StreamChat AI API
    /// accepts at POST /livesplit/events.
    ///
    /// ⚠️ A negative delta is AHEAD of the personal best.
    /// </summary>
    public static class EventBuilder
    {
        public static Dictionary<string, object> Start(RunSnapshot run, string runId)
        {
            var payload = Base("start", run, runId);
            payload["current"] = Current(run, 0);
            return payload;
        }

        /// <summary>
        /// A split. <paramref name="index"/> is the split just completed; when
        /// it is the last one the run has finished and this becomes a finish.
        /// </summary>
        public static Dictionary<string, object> Split(RunSnapshot run, string runId, int index)
        {
            var isLast = index >= run.Segments.Count - 1;
            var payload = Base(isLast ? "finish" : "split", run, runId);
            payload["split"] = SplitDetail(run, index);
            payload["current"] = Current(run, index + 1);

            if (isLast)
            {
                var final = run.Segments[index].SplitMs;
                var previousPb = PbMs(run);
                payload["finish"] = new Dictionary<string, object>
                {
                    ["final_ms"] = final,
                    ["previous_pb_ms"] = previousPb,
                    // A first ever finish is a personal best by definition.
                    ["is_pb"] = final.HasValue && (!previousPb.HasValue || final.Value < previousPb.Value),
                };
            }

            return payload;
        }

        public static Dictionary<string, object> Skip(RunSnapshot run, string runId, int index)
        {
            var payload = Base("skip", run, runId);
            payload["split"] = new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = Name(run, index),
            };
            payload["current"] = Current(run, index + 1);
            return payload;
        }

        /// <summary><paramref name="index"/> is the split that was undone, and is current again.</summary>
        public static Dictionary<string, object> Undo(RunSnapshot run, string runId, int index)
        {
            var payload = Base("undo", run, runId);
            payload["split"] = new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = Name(run, index),
            };
            payload["current"] = Current(run, index);
            return payload;
        }

        /// <summary>
        /// LiveSplit has already cleared the splits by the time it says so,
        /// which is why the index and time come from what the component last saw.
        /// </summary>
        public static Dictionary<string, object> Reset(RunSnapshot run, string runId, string fromPhase, int lastIndex, long? runTimeMs)
        {
            var payload = Base("reset", run, runId);
            payload["current"] = Current(run, lastIndex);
            payload["reset"] = new Dictionary<string, object>
            {
                ["from_phase"] = fromPhase,
                ["run_time_ms"] = runTimeMs,
            };
            return payload;
        }

        public static Dictionary<string, object> Pause(RunSnapshot run, string runId) => Base("pause", run, runId);

        public static Dictionary<string, object> Resume(RunSnapshot run, string runId) => Base("resume", run, runId);

        /// <summary>
        /// The time for segment <paramref name="index"/> alone. Null when the
        /// split before it was skipped: the time then covers two segments, and
        /// calling that a gold would be a lie.
        /// </summary>
        public static long? SegmentMs(RunSnapshot run, int index)
        {
            var split = run.Segments[index].SplitMs;
            if (!split.HasValue)
            {
                return null;
            }

            if (index == 0)
            {
                return split;
            }

            var previous = run.Segments[index - 1].SplitMs;
            return previous.HasValue ? split - previous : null;
        }

        public static bool IsGold(RunSnapshot run, int index)
        {
            var segment = SegmentMs(run, index);
            var best = run.Segments[index].BestSegmentMs;

            // No best yet means the first time through: every split would be
            // "gold", which says nothing.
            return segment.HasValue && best.HasValue && segment.Value < best.Value;
        }

        public static long? SumOfBestMs(RunSnapshot run)
        {
            long total = 0;
            foreach (var segment in run.Segments)
            {
                if (!segment.BestSegmentMs.HasValue)
                {
                    return null;
                }
                total += segment.BestSegmentMs.Value;
            }
            return run.Segments.Count == 0 ? (long?)null : total;
        }

        private static long? PbMs(RunSnapshot run) =>
            run.Segments.Count == 0 ? null : run.Segments[run.Segments.Count - 1].PbSplitMs;

        private static Dictionary<string, object> Base(string eventName, RunSnapshot run, string runId)
        {
            return new Dictionary<string, object>
            {
                ["event"] = eventName,
                ["event_id"] = Guid.NewGuid().ToString(),
                ["run_id"] = runId,
                ["run"] = new Dictionary<string, object>
                {
                    ["game"] = Limit(run.Game),
                    ["category"] = Limit(run.Category),
                    ["attempt"] = run.Attempt,
                    ["split_count"] = run.Segments.Count,
                    ["timing_method"] = run.TimingMethod,
                    ["pb_ms"] = PbMs(run),
                    ["sum_of_best_ms"] = SumOfBestMs(run),
                },
            };
        }

        private static Dictionary<string, object> SplitDetail(RunSnapshot run, int index)
        {
            var segment = run.Segments[index];
            var delta = segment.SplitMs.HasValue && segment.PbSplitMs.HasValue
                ? segment.SplitMs.Value - segment.PbSplitMs.Value
                : (long?)null;

            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = Name(run, index),
                ["time_ms"] = segment.SplitMs,
                ["segment_ms"] = SegmentMs(run, index),
                ["delta_ms"] = delta,
                ["pb_split_ms"] = segment.PbSplitMs,
                ["previous_best_segment_ms"] = segment.BestSegmentMs,
                ["gold"] = IsGold(run, index),
            };
        }

        private static Dictionary<string, object> Current(RunSnapshot run, int index)
        {
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = index >= 0 && index < run.Segments.Count ? Name(run, index) : null,
            };
        }

        /// <summary>
        /// Subsplits are named "-Chapter 1" or "{Chapter 1}Summit" in LiveSplit;
        /// chat wants the plain name.
        /// </summary>
        public static string Name(RunSnapshot run, int index)
        {
            var name = (run.Segments[index].Name ?? string.Empty).Trim();

            if (name.StartsWith("-", StringComparison.Ordinal))
            {
                name = name.Substring(1);
            }

            if (name.StartsWith("{", StringComparison.Ordinal))
            {
                var close = name.IndexOf('}');
                if (close > 0 && close < name.Length - 1)
                {
                    name = name.Substring(close + 1);
                }
            }

            return Limit(name.Trim());
        }

        private static string Limit(string value)
        {
            if (value == null)
            {
                return null;
            }
            value = value.Trim();
            return value.Length > 128 ? value.Substring(0, 128) : value;
        }
    }
}
