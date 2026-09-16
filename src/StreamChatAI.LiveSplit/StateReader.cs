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
            var method = state.CurrentTimingMethod;
            var snapshot = new RunSnapshot
            {
                Game = state.Run.GameName,
                Category = state.Run.CategoryName,
                Attempt = state.Run.AttemptCount,
                TimingMethod = method == TimingMethod.GameTime ? "GameTime" : "RealTime",
            };

            foreach (var segment in state.Run)
            {
                snapshot.Segments.Add(new SegmentSnapshot
                {
                    Name = segment.Name,
                    SplitMs = Ms(segment.SplitTime[method]),
                    PbSplitMs = Ms(segment.PersonalBestSplitTime[method]),
                    BestSegmentMs = Ms(segment.BestSegmentTime[method]),
                });
            }

            return snapshot;
        }

        public static long? CurrentTimeMs(LiveSplitState state) => Ms(state.CurrentTime[state.CurrentTimingMethod]);

        private static long? Ms(TimeSpan? time) => time.HasValue ? (long)Math.Round(time.Value.TotalMilliseconds) : (long?)null;
    }
}
