using System.Xml;

namespace StreamChatAI.LiveSplit
{
    /// <summary>
    /// What is saved in the layout. Never the token - see TokenStore - and
    /// never the address it is sent to: a layout is shared between runners,
    /// and one that pointed the component somewhere else would hand that
    /// server the token with every split.
    ///
    /// There are no per-event switches on purpose. Which events do something
    /// in chat is chosen on the website's Reactions page; leaving an event out
    /// here would only leave the run log and $(livesplit) believing a run is
    /// still going after it was reset.
    /// </summary>
    public sealed class ComponentSettings
    {
        public bool Enabled { get; set; } = true;

        public XmlNode ToXml(XmlDocument document)
        {
            var parent = document.CreateElement("Settings");
            Add(document, parent, "Version", "1");
            Add(document, parent, "Enabled", Enabled.ToString());
            return parent;
        }

        public void FromXml(XmlNode node)
        {
            if (node == null)
            {
                return;
            }
            Enabled = !bool.TryParse(node["Enabled"]?.InnerText, out var enabled) || enabled;
        }

        public int Hash() => Enabled ? 1 : 0;

        private static void Add(XmlDocument document, XmlElement parent, string name, string value)
        {
            var element = document.CreateElement(name);
            element.InnerText = value;
            parent.AppendChild(element);
        }
    }
}
