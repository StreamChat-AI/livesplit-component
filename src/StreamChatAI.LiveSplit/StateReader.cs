using System;
using LiveSplit.Model;
using StreamChatAI.LiveSplit.Core;

namespace StreamChatAI.LiveSplit
{
    /// <summary>
    /// Copies what an event needs out of LiveSplit's live objects, on the UI
    /// thread, before anything is handed to the sender.
    /// </summary>
    internal static class StateReader
    {
        public static RunSnapshot Read(LiveSplitState state)
        {
            var method = EffectiveMethod(state);
            var snapshot = new RunSnapshot
            {
                Game = state.Run.GameName,
                Category = state.Run.CategoryName,
                Attempt = state.Run.AttemptCount,
                TimingMethod = method == TimingMethod.GameTime ? "GameTime" : "RealTime",
            };

            try
            {
                foreach (var pair in state.Run.Metadata?.VariableValueNames ?? new System.Collections.Generic.Dictionary<string, string>())
                {
                    snapshot.Variables[pair.Key] = pair.Value;
                }
            }
            catch (Exception)
            {
                // Metadata can reach for speedrun.com; a run is worth sending without it.
            }

            foreach (var segment in state.Run)
            {
                snapshot.Segments.Add(new SegmentSnapshot
                {
                    Name = segment.Name,
                    SplitMs = Ms(segment.SplitTime[method]),
                    PbSplitMs = Ms(segment.PersonalBestSplitTime[method]),
                    BestSegmentMs = Ms(segment.BestSegmentTime[method]),
                    SplitRealMs = Ms(segment.SplitTime.RealTime),
                    SplitGameMs = Ms(segment.SplitTime.GameTime),
                });
            }

            return snapshot;
        }

        public static long? CurrentTimeMs(LiveSplitState state) => Ms(state.CurrentTime[EffectiveMethod(state)]);

        /// <summary>
        /// Game Time with nothing feeding it - no load remover or auto splitter
        /// - is null for every split, which would send a run with no times, no
        /// golds and never a PB. Real Time is what the runner is actually
        /// looking at then, so that is what is sent, and said to be.
        /// </summary>
        private static TimingMethod EffectiveMethod(LiveSplitState state)
        {
            return state.CurrentTimingMethod == TimingMethod.GameTime && !state.IsGameTimeInitialized
                ? TimingMethod.RealTime
                : state.CurrentTimingMethod;
        }

        private static long? Ms(TimeSpan? time) => time.HasValue ? (long)Math.Round(time.Value.TotalMilliseconds) : (long?)null;
    }
}
