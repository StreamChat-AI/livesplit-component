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
    }
}
