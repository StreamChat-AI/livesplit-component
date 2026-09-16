using System;
using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(StreamChatAI.LiveSplit.StreamChatAIFactory))]

namespace StreamChatAI.LiveSplit
{
    public sealed class StreamChatAIFactory : IComponentFactory
    {
        public const string Name = "StreamChat AI";

        public string ComponentName => Name;

        public string Description => "Sends your splits, golds, personal bests and resets to StreamChat AI, so your chat bot can react to your run.";

        public ComponentCategory Category => ComponentCategory.Other;

        public IComponent Create(LiveSplitState state) => new StreamChatAIComponent(state);

        public string UpdateName => ComponentName;

        public string XMLURL => UpdateURL + "update.StreamChatAI.xml";

        public string UpdateURL => "https://raw.githubusercontent.com/StreamChat-AI/livesplit-component/main/";

        public Version Version => typeof(StreamChatAIFactory).Assembly.GetName().Version;
    }
}
