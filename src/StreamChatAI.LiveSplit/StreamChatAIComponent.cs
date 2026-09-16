using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using StreamChatAI.LiveSplit.Core;

namespace StreamChatAI.LiveSplit
{
    public enum ConnectionStatus
    {
        NotConnected,
        Checking,
        WaitingForCode,
        Connected,
    }

    /// <summary>
    /// An invisible layout component: it draws nothing and only listens to the
    /// timer.
    /// </summary>
    public sealed class StreamChatAIComponent : IComponent
    {
        private readonly LiveSplitState _state;
        private readonly ComponentSettings _settings = new ComponentSettings();
        private readonly string _version;
        private ApiClient _api;
        private readonly EventSender _sender;
        private SettingsControl _control;
        private CancellationTokenSource _pairing;

        private volatile string _token;
        private string _runId;
        private int _lastIndex = -1;
        private long? _lastTimeMs;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.NotConnected;
        public string Account { get; private set; }
        public string PairingCode { get; private set; }
        public string PairingUrl { get; private set; }
        public string LastError { get; private set; }

        /// <summary>Raised from any thread. The settings control marshals it.</summary>
        public event Action StatusChanged;

        public StreamChatAIComponent(LiveSplitState state)
        {
            _state = state;
            _version = typeof(StreamChatAIComponent).Assembly.GetName().Version.ToString(3);
            // ⚠️ The address comes from the machine, never from the layout:
            // see ComponentSettings. Set STREAMCHATAI_API_URL to develop
            // against a local API.
            _api = new ApiClient(Environment.GetEnvironmentVariable("STREAMCHATAI_API_URL"), _version);
            _sender = new EventSender((token, json, cancel) => _api.SendEventAsync(token, json, cancel), () => _settings.Enabled ? _token : null);
            _sender.Unauthorized += OnTokenRevoked;

            _state.OnStart += OnStart;
            _state.OnSplit += OnSplit;
            _state.OnSkipSplit += OnSkipSplit;
            _state.OnUndoSplit += OnUndoSplit;
            _state.OnReset += OnReset;
            _state.OnPause += OnPause;
            _state.OnResume += OnResume;

            _token = TokenStore.Load();
            if (_token != null)
            {
                _ = CheckTokenAsync();
            }
        }

        public string ComponentName => StreamChatAIFactory.Name;

        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;

        public IDictionary<string, Action> ContextMenuControls => null;

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
        }

        public Control GetSettingsControl(LayoutMode mode)
        {
            if (_control == null || _control.IsDisposed)
            {
                _control = new SettingsControl(this, _settings);
            }
            return _control;
        }

        public XmlNode GetSettings(XmlDocument document) => _settings.ToXml(document);

        public void SetSettings(XmlNode settings) => _settings.FromXml(settings);

        public int GetSettingsHashCode() => _settings.Hash();

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            // LiveSplit clears the splits before it announces a reset, so the
            // reset has to be described from what was last seen here.
            if (state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused)
            {
                _lastTimeMs = StateReader.CurrentTimeMs(state);
                _lastIndex = state.CurrentSplitIndex;

                // Added to the layout, or connected, in the middle of a run.
                _runId ??= Guid.NewGuid().ToString();
            }
        }

        // --- timer events: all on LiveSplit's UI thread, so they only copy and queue

        private void OnStart(object sender, EventArgs e)
        {
            _runId = Guid.NewGuid().ToString();
            _lastIndex = 0;
            _lastTimeMs = 0;
            Queue(EventBuilder.Start(StateReader.Read(_state), _runId));
        }

        private void OnSplit(object sender, EventArgs e)
        {
            var index = _state.CurrentSplitIndex - 1;
            if (index < 0)
            {
                return;
            }
            _lastIndex = _state.CurrentSplitIndex;
            Queue(EventBuilder.Split(StateReader.Read(_state), RunId(), index));
        }

        private void OnSkipSplit(object sender, EventArgs e)
        {
            var index = _state.CurrentSplitIndex - 1;
            if (index < 0)
            {
                return;
            }
            _lastIndex = _state.CurrentSplitIndex;
            Queue(EventBuilder.Skip(StateReader.Read(_state), RunId(), index));
        }

        private void OnUndoSplit(object sender, EventArgs e)
        {
            _lastIndex = _state.CurrentSplitIndex;
            Queue(EventBuilder.Undo(StateReader.Read(_state), RunId(), _state.CurrentSplitIndex));
        }

        private void OnReset(object sender, TimerPhase previousPhase)
        {
            Queue(EventBuilder.Reset(StateReader.Read(_state), RunId(), previousPhase.ToString(), Math.Max(0, _lastIndex), _lastTimeMs));
            _runId = null;
            _lastIndex = -1;
            _lastTimeMs = null;
        }

        private void OnPause(object sender, EventArgs e) => Queue(EventBuilder.Pause(StateReader.Read(_state), RunId()));

        private void OnResume(object sender, EventArgs e) => Queue(EventBuilder.Resume(StateReader.Read(_state), RunId()));

        private string RunId() => _runId ??= Guid.NewGuid().ToString();

        private void Queue(Dictionary<string, object> payload)
        {
            try
            {
                _sender.Enqueue(Json.Serialize(payload));
            }
            catch (Exception)
            {
                // Nothing this component does may ever take the timer down.
            }
        }

        // --- connecting

        public async Task ConnectAsync()
        {
            CancelPairing();
            var cancel = new CancellationTokenSource();
            _pairing = cancel;
            LastError = null;
            SetStatus(ConnectionStatus.Checking);

            try
            {
                var start = await _api.StartPairingAsync(Environment.MachineName, cancel.Token).ConfigureAwait(false);
                PairingCode = start.Code;
                PairingUrl = start.PairUrl;
                SetStatus(ConnectionStatus.WaitingForCode);

                var expires = DateTime.UtcNow.AddSeconds(start.ExpiresInSeconds);
                while (!cancel.IsCancellationRequested && DateTime.UtcNow < expires)
                {
                    await Task.Delay(TimeSpan.FromSeconds(start.PollIntervalSeconds), cancel.Token).ConfigureAwait(false);

                    PairingPoll poll;
                    try
                    {
                        poll = await _api.PollPairingAsync(start.Secret, cancel.Token).ConfigureAwait(false);
                    }
                    catch (Exception) when (!cancel.IsCancellationRequested)
                    {
                        continue; // a dropped poll is not a failed pairing
                    }

                    if (poll.Status == "connected" && !string.IsNullOrEmpty(poll.Token))
                    {
                        TokenStore.Save(poll.Token);
                        _token = poll.Token;
                        Account = poll.Account;
                        PairingCode = null;
                        SetStatus(ConnectionStatus.Connected);
                        return;
                    }

                    if (poll.Status == "expired")
                    {
                        break;
                    }
                }

                if (!cancel.IsCancellationRequested)
                {
                    LastError = "The code expired before it was entered. Press Connect for a new one.";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                LastError = "Could not reach StreamChat AI. Check your internet connection and try again.";
            }

            PairingCode = null;
            if (_token == null)
            {
                SetStatus(ConnectionStatus.NotConnected);
            }
        }

        public void CancelPairing()
        {
            var pairing = _pairing;
            _pairing = null;
            pairing?.Cancel();
        }

        public async Task DisconnectAsync()
        {
            var token = _token;
            _token = null;
            Account = null;
            TokenStore.Clear();
            SetStatus(ConnectionStatus.NotConnected);

            if (token != null)
            {
                try
                {
                    await _api.DisconnectAsync(token, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Forgotten locally either way; the website can revoke it too.
                }
            }
        }

        private async Task CheckTokenAsync()
        {
            SetStatus(ConnectionStatus.Checking);
            try
            {
                var account = await _api.WhoAmIAsync(_token, CancellationToken.None).ConfigureAwait(false);
                if (account == null)
                {
                    OnTokenRevoked();
                    return;
                }
                Account = account;
                SetStatus(ConnectionStatus.Connected);
            }
            catch (Exception)
            {
                // Offline at start-up. Keep the token and try sending anyway.
                SetStatus(ConnectionStatus.Connected);
            }
        }

        private void OnTokenRevoked()
        {
            _token = null;
            Account = null;
            TokenStore.Clear();
            LastError = "This copy of LiveSplit was disconnected on the website. Press Connect to link it again.";
            SetStatus(ConnectionStatus.NotConnected);
        }

        private void SetStatus(ConnectionStatus status)
        {
            Status = status;
            try
            {
                StatusChanged?.Invoke();
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            _state.OnStart -= OnStart;
            _state.OnSplit -= OnSplit;
            _state.OnSkipSplit -= OnSkipSplit;
            _state.OnUndoSplit -= OnUndoSplit;
            _state.OnReset -= OnReset;
            _state.OnPause -= OnPause;
            _state.OnResume -= OnResume;
            CancelPairing();
            _sender.Dispose();
            _api.Dispose();
            _control?.Dispose();
        }
    }
}
