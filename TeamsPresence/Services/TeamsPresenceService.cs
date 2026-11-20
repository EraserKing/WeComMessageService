using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using TeamsPresence.Exceptions;
using TeamsPresence.Models;
using TeamsPresence.Models.Configurations;

namespace TeamsPresence.Services
{
    /// <summary>
    /// Client for the teams-ctl HTTP API (teams-ctl.py on the teams-presence host).
    /// Every call authenticates with the control token in the query string and
    /// returns only file-backed state, so the calls are cheap and side-effect free
    /// apart from pause/resume, status and watch-list edits.
    /// </summary>
    public class TeamsPresenceService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private readonly ILogger<TeamsPresenceService> Logger;
        private readonly TeamsPresenceConfiguration Configuration;
        private readonly HttpClient Client;

        public TeamsPresenceService(IOptions<TeamsPresenceConfiguration> options, ILogger<TeamsPresenceService> logger)
        {
            Configuration = options.Value;
            Logger = logger;
            Client = new HttpClient { Timeout = RequestTimeout };
        }

        public Task<TeamsPresenceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return SendAsync<TeamsPresenceStatus>(HttpMethod.Get, "/status", cancellationToken: cancellationToken);
        }

        public Task<TeamsPresenceWatch> GetWatchAsync(CancellationToken cancellationToken = default)
        {
            return SendAsync<TeamsPresenceWatch>(HttpMethod.Get, "/watch", cancellationToken: cancellationToken);
        }

        public async Task<bool> PauseAsync(CancellationToken cancellationToken = default)
        {
            var result = await SendAsync<TeamsPresencePauseResult>(HttpMethod.Post, "/pause", cancellationToken: cancellationToken);
            return result.Paused;
        }

        public async Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
        {
            var result = await SendAsync<TeamsPresencePauseResult>(HttpMethod.Post, "/resume", cancellationToken: cancellationToken);
            return result.Paused;
        }

        public async Task<string> SetStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            if (status is not ("available" or "busy" or "away"))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "status must be available, busy or away");
            }

            var result = await SendAsync<TeamsPresenceManualResult>(HttpMethod.Post, $"/status/{status}", cancellationToken: cancellationToken);
            return result.Manual ?? status;
        }

        public Task<TeamsPresenceWatch> EditWatchAsync(
            TeamsPresenceWatchList list,
            TeamsPresenceWatchAction action,
            string name,
            CancellationToken cancellationToken = default)
        {
            name = (name ?? string.Empty).Trim();
            var nameLength = name.EnumerateRunes().Count();
            if (nameLength is < 1 or > 64 || name.Contains(',') || name.Contains('\n') || name.Contains('\r'))
            {
                throw new InvalidOperationException("name 必须为 1-64 个字符且不能包含逗号。");
            }

            var listSegment = list == TeamsPresenceWatchList.Senders ? "senders" : "keywords";
            var actionSegment = action == TeamsPresenceWatchAction.Add ? "add" : "remove";
            return SendAsync<TeamsPresenceWatch>(
                HttpMethod.Post,
                $"/watch/{listSegment}/{actionSegment}",
                [("name", name)],
                cancellationToken);
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string path,
            (string Key, string? Value)[]? query = null,
            CancellationToken cancellationToken = default)
        {
            var uri = BuildUri(path, query ?? []);
            Logger.LogDebug("TEAMSPRESENCE: {Method} {Path}", method, path);

            using var request = new HttpRequestMessage(method, uri);
            HttpResponseMessage response;
            try
            {
                response = await Client.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"TeamsPresence API 请求超时（{RequestTimeout.TotalSeconds:0} 秒）：{Configuration.BaseUrl}", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"无法连接 TeamsPresence API：{Configuration.BaseUrl}", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw CreateException(response.StatusCode, body, path);
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(body, JsonOptions)
                        ?? throw new InvalidOperationException($"TeamsPresence 返回了空响应：{path}");
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"TeamsPresence 返回了无法解析的响应：{Truncate(body)}", ex);
                }
            }
        }

        private Uri BuildUri(string path, (string Key, string? Value)[] query)
        {
            EnsureConfigured();

            var builder = new StringBuilder(Configuration.BaseUrl.TrimEnd('/'));
            builder.Append(path).Append("?token=").Append(Uri.EscapeDataString(Configuration.Token));
            foreach (var (key, value) in query)
            {
                if (value is null)
                {
                    continue;
                }

                builder.Append('&')
                       .Append(Uri.EscapeDataString(key))
                       .Append('=')
                       .Append(Uri.EscapeDataString(value));
            }

            return new Uri(builder.ToString());
        }

        private void EnsureConfigured()
        {
            if (!Configuration.IsEnabled)
            {
                throw new InvalidOperationException("TeamsPresence 服务已禁用（TeamsPresence:IsEnabled）。");
            }

            if (string.IsNullOrWhiteSpace(Configuration.BaseUrl)
                || !Uri.TryCreate(Configuration.BaseUrl, UriKind.Absolute, out var baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"TeamsPresence:BaseUrl 不是有效的 HTTP 地址：{Configuration.BaseUrl}");
            }

            if (string.IsNullOrWhiteSpace(Configuration.Token))
            {
                throw new InvalidOperationException("未配置 TeamsPresence:Token（teams-presence 主机 state/ctl-token 的内容）。");
            }
        }

        private static TeamsPresenceApiException CreateException(HttpStatusCode statusCode, string body, string path)
        {
            var detail = ExtractError(body);
            var code = (int)statusCode;
            var message = statusCode switch
            {
                HttpStatusCode.Forbidden => $"TeamsPresence 控制令牌无效（403）：{detail ?? "bad token"}。请检查 TeamsPresence:Token。",
                HttpStatusCode.NotFound => $"TeamsPresence API 不存在：{path}（404）。",
                HttpStatusCode.BadRequest => $"TeamsPresence 拒绝了请求：{detail ?? "400"}。",
                _ => $"TeamsPresence 返回 HTTP {code}：{detail ?? "未知错误"}。",
            };

            return new TeamsPresenceApiException(message, code);
        }

        private static string? ExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }
            }
            catch (JsonException)
            {
                // Fall through to the raw body.
            }

            return Truncate(body);
        }

        private static string Truncate(string value, int maxLength = 300)
        {
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
