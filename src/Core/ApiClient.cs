using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamChatAI.LiveSplit.Core
{
    public enum SendResult
    {
        Sent,
        /// <summary>The token was revoked or never valid. Stop and forget it.</summary>
        Unauthorized,
        /// <summary>The server refused this event as malformed. Retrying will not help.</summary>
        Rejected,
        /// <summary>Network trouble or a server error. Worth retrying.</summary>
        Failed,
    }

    public sealed class PairingStart
    {
        public string Code { get; set; }
        public string Secret { get; set; }
        public int ExpiresInSeconds { get; set; }
        public int PollIntervalSeconds { get; set; }
        public string PairUrl { get; set; }
    }

    public sealed class PairingPoll
    {
        /// <summary>pending, expired or connected.</summary>
        public string Status { get; set; }
        public string Token { get; set; }
        public string Account { get; set; }
    }

    /// <summary>
    /// The StreamChat AI API, as the component uses it.
    /// </summary>
    public sealed class ApiClient : IDisposable
    {
        public const string DefaultBaseUrl = "https://api.streamchatai.com";

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _version;

        public ApiClient(string baseUrl, string componentVersion, HttpMessageHandler handler = null)
        {
            _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl).TrimEnd('/');
            _version = componentVersion;
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.Add("X-Component-Version", componentVersion);
        }

        public async Task<PairingStart> StartPairingAsync(string deviceName, CancellationToken cancel)
        {
            var body = new Dictionary<string, object> { ["device_name"] = deviceName, ["component_version"] = _version };
            var response = await SendAsync(HttpMethod.Post, "/livesplit/pair", null, body, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var fields = Json.ParseFlatObject(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            return new PairingStart
            {
                Code = Get(fields, "code"),
                Secret = Get(fields, "secret"),
                ExpiresInSeconds = GetInt(fields, "expires_in", 600),
                PollIntervalSeconds = Math.Max(1, GetInt(fields, "poll_interval", 3)),
                PairUrl = Get(fields, "pair_url") ?? "https://www.streamchatai.com/b/livesplit",
            };
        }

        public async Task<PairingPoll> PollPairingAsync(string secret, CancellationToken cancel)
        {
            var body = new Dictionary<string, object> { ["secret"] = secret };
            var response = await SendAsync(HttpMethod.Post, "/livesplit/pair/poll", null, body, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var fields = Json.ParseFlatObject(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

            return new PairingPoll
            {
                Status = Get(fields, "status") ?? "expired",
                Token = Get(fields, "token"),
                Account = Get(fields, "account"),
            };
        }

        /// <summary>Which account the token belongs to, or null when it no longer works.</summary>
        public async Task<string> WhoAmIAsync(string token, CancellationToken cancel)
        {
            var response = await SendAsync(HttpMethod.Get, "/livesplit/me", token, null, cancel).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return Get(Json.ParseFlatObject(await response.Content.ReadAsStringAsync().ConfigureAwait(false)), "account");
        }

        public async Task DisconnectAsync(string token, CancellationToken cancel)
        {
            using (var response = await SendAsync(HttpMethod.Delete, "/livesplit/me", token, null, cancel).ConfigureAwait(false))
            {
                // Already gone is the outcome we wanted.
            }
        }

        public async Task<SendResult> SendEventAsync(string token, string json, CancellationToken cancel)
        {
            try
            {
                using (var response = await SendRawAsync(HttpMethod.Post, "/livesplit/events", token, json, cancel).ConfigureAwait(false))
                {
                    var code = (int)response.StatusCode;
                    if (code == 401)
                    {
                        return SendResult.Unauthorized;
                    }
                    if (code == 422 || code == 400)
                    {
                        return SendResult.Rejected;
                    }
                    return response.IsSuccessStatusCode ? SendResult.Sent : SendResult.Failed;
                }
            }
            catch (HttpRequestException)
            {
                return SendResult.Failed;
            }
            catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
            {
                return SendResult.Failed; // timed out
            }
        }

        private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, Dictionary<string, object> body, CancellationToken cancel)
        {
            return SendRawAsync(method, path, token, body == null ? null : Json.Serialize(body), cancel);
        }

        private Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, string token, string json, CancellationToken cancel)
        {
            var request = new HttpRequestMessage(method, _baseUrl + path);
            if (token != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            if (json != null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return _http.SendAsync(request, cancel);
        }

        private static string Get(Dictionary<string, string> fields, string key) =>
            fields.TryGetValue(key, out var value) ? value : null;

        private static int GetInt(Dictionary<string, string> fields, string key, int fallback) =>
            fields.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

        public void Dispose() => _http.Dispose();
    }
}
