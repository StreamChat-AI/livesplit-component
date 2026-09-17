using System.Collections.Generic;

namespace StreamChatAI.LiveSplit.Core
{
    /// <summary>
    /// What the component read out of LiveSplit at the moment of an event.
    /// Plain data, so the payload logic can be tested without LiveSplit.
    /// All times are milliseconds in the runner's chosen timing method.
    /// </summary>
    public sealed class RunSnapshot
    {
        public string Game { get; set; }
        public string Category { get; set; }
        public int Attempt { get; set; }

        /// <summary>"RealTime" or "GameTime".</summary>
        public string TimingMethod { get; set; }

        public IList<SegmentSnapshot> Segments { get; set; } = new List<SegmentSnapshot>();

        /// <summary>
        /// speedrun.com sub-categories as set in LiveSplit's splits editor:
        /// variable name to value label. Lets the API find the right board
        /// to check a world record against.
        /// </summary>
        public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }

    public sealed class SegmentSnapshot
    {
        public string Name { get; set; }

        /// <summary>This attempt's split time. Null when not reached or skipped.</summary>
        public long? SplitMs { get; set; }

        /// <summary>The personal best's split time here.</summary>
        public long? PbSplitMs { get; set; }

        /// <summary>
        /// The best segment before this attempt. LiveSplit only folds a new
        /// best in when the run is reset, so during the run this is still the
        /// old one - which is what makes a gold detectable.
        /// </summary>
        public long? BestSegmentMs { get; set; }

        /// <summary>
        /// This attempt's split on both clocks, whatever the runner is
        /// comparing against. speedrun.com rank some boards on real time and
        /// some on load-removed or in-game time.
        /// </summary>
        public long? SplitRealMs { get; set; }
        public long? SplitGameMs { get; set; }
    }
}
