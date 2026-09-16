using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace StreamChatAI.LiveSplit
{
    /// <summary>
    /// The StreamChat AI tab in Layout Settings. Built in code rather than with
    /// the designer so the whole component stays a handful of readable files.
    /// </summary>
    public sealed class SettingsControl : UserControl
    {
        private readonly StreamChatAIComponent _component;
        private readonly ComponentSettings _settings;

        private readonly Label _status = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly Label _detail = new Label { AutoSize = true, MaximumSize = new Size(440, 0) };
        private readonly Label _code = new Label { AutoSize = true, Font = new Font(FontFamily.GenericMonospace, 24f, FontStyle.Bold), Visible = false };
        private readonly LinkLabel _link = new LinkLabel { AutoSize = true, Visible = false };
        private readonly Button _connect = new Button { AutoSize = true, Text = "Connect" };
        private readonly Button _disconnect = new Button { AutoSize = true, Text = "Disconnect" };
        private readonly Button _cancel = new Button { AutoSize = true, Text = "Cancel", Visible = false };
        private readonly CheckBox _enabled = new CheckBox { AutoSize = true, Text = "Send my run to StreamChat AI" };

        public SettingsControl(StreamChatAIComponent component, ComponentSettings settings)
        {
            _component = component;
            _settings = settings;

            Padding = new Padding(7);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var intro = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(440, 0),
                Text = "Lets your StreamChat AI bot react to your splits, golds, personal bests and resets. Choose what it says on the Reactions page of the StreamChat AI website.",
            };

            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            buttons.Controls.AddRange(new Control[] { _connect, _disconnect, _cancel });

            var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
            layout.Controls.AddRange(new Control[] { intro, Spacer(), _status, _detail, _code, _link, buttons, Spacer(), _enabled });
            Controls.Add(layout);

            _enabled.Checked = _settings.Enabled;
            _enabled.CheckedChanged += (s, e) => _settings.Enabled = _enabled.Checked;
            _connect.Click += async (s, e) => await _component.ConnectAsync();
            _cancel.Click += (s, e) => _component.CancelPairing();
            _disconnect.Click += async (s, e) =>
            {
                var sure = MessageBox.Show(this, "Disconnect this copy of LiveSplit from StreamChat AI?", "StreamChat AI", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (sure == DialogResult.Yes)
                {
                    await _component.DisconnectAsync();
                }
            };
            _link.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_component.PairingUrl) { UseShellExecute = true });
                }
                catch (Exception)
                {
                }
            };

            _component.StatusChanged += OnStatusChanged;
            Render();
        }

        private static Control Spacer() => new Panel { Height = 8, Width = 1 };

        private void OnStatusChanged()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke((Action)Render);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void Render()
        {
            var status = _component.Status;
            var waiting = status == ConnectionStatus.WaitingForCode;

            _connect.Visible = status == ConnectionStatus.NotConnected;
            _disconnect.Visible = status == ConnectionStatus.Connected;
            _cancel.Visible = waiting;
            _code.Visible = waiting;
            _link.Visible = waiting;

            switch (status)
            {
                case ConnectionStatus.Connected:
                    _status.Text = "Connected" + (string.IsNullOrEmpty(_component.Account) ? "" : " as " + _component.Account);
                    // Somebody who read the code off stream and typed it first
                    // shows up here, which is why the name is spelled out.
                    _detail.Text = "Not your account? Press Disconnect, then Connect again.";
                    break;
                case ConnectionStatus.Checking:
                    _status.Text = "Checking...";
                    _detail.Text = "";
                    break;
                case ConnectionStatus.WaitingForCode:
                    _status.Text = "Enter this code on the StreamChat AI website:";
                    _code.Text = _component.PairingCode;
                    _link.Text = _component.PairingUrl;
                    _detail.Text = "Keep this window off stream until you have entered it - anyone who sees the code could use it first.";
                    break;
                default:
                    _status.Text = "Not connected";
                    _detail.Text = _component.LastError ?? "Press Connect to link LiveSplit to your StreamChat AI account.";
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _component.StatusChanged -= OnStatusChanged;
            }
            base.Dispose(disposing);
        }
    }
}
