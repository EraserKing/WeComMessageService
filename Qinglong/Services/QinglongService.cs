using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qinglong.Exceptions;
using Qinglong.Models;
using Qinglong.Models.Configurations;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Qinglong.Services
{
    public class QinglongService
    {
        private readonly ILogger<QinglongService> Logger;
        private readonly IOptions<QinglongServiceConfiguration> Options;

        private readonly record struct QinglongAuth(string Token, string TokenType, long ExpirationUnixSeconds);

        private static readonly HttpClient HttpClient = new();
        private static readonly SemaphoreSlim LoginLock = new(1, 1);
        private static QinglongAuth? CachedAuth;

        public QinglongService(ILogger<QinglongService> logger, IOptions<QinglongServiceConfiguration> options)
        {
            Logger = logger;
            Options = options;
            if (HttpClient.BaseAddress == null && !string.IsNullOrEmpty(options.Value.SiteUrl))
            {
                HttpClient.BaseAddress = new Uri(options.Value.SiteUrl);
            }
        }

        public bool IsCommandValid(string command) => Options.Value.Commands?.ContainsKey(command) ?? false;

        private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static bool IsValid(QinglongAuth auth) =>
            !string.IsNullOrEmpty(auth.Token) && auth.ExpirationUnixSeconds > UnixNow();

        private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string uri, QinglongAuth auth, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, uri)
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(auth.TokenType, auth.Token);
            return request;
        }

        private async Task<QinglongAuth> GetAuthAsync()
        {
            if (string.IsNullOrEmpty(Options.Value.SiteUrl) || string.IsNullOrEmpty(Options.Value.ClientId) || string.IsNullOrEmpty(Options.Value.ClientSecret))
            {
                Logger.LogError("QINGLONG: Site, client ID, or client password is not set");
                throw new LoginException("Site, client ID, or client password is not set");
            }

            await LoginLock.WaitAsync();
            try
            {
                if (CachedAuth is { } cached && IsValid(cached))
                {
                    Logger.LogInformation("QINGLONG: Token is still valid");
                    return cached;
                }

                CachedAuth = null;
                Logger.LogInformation("QINGLONG: Ready to log in");

                using var authRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    QueryHelpers.AddQueryString("/open/auth/token", new Dictionary<string, string?>
                    {
                        ["client_id"] = Options.Value.ClientId,
                        ["client_secret"] = Options.Value.ClientSecret,
                    }));
                var authMessage = await HttpClient.SendAsync(authRequest);
                QinglongAuthTokenModel? authResponse = await authMessage.Content.ReadFromJsonAsync<QinglongAuthTokenModel>();
                if (authResponse == null)
                {
                    Logger.LogError("QINGLONG: Log in response is null");
                    throw new LoginException("Log in response is null");
                }
                else if (authResponse.code == 200
                    && authResponse.data != null
                    && !string.IsNullOrEmpty(authResponse.data.token)
                    && authResponse.data.expiration > UnixNow())
                {
                    var auth = new QinglongAuth(
                        authResponse.data.token,
                        string.IsNullOrEmpty(authResponse.data.token_type) ? "Bearer" : authResponse.data.token_type,
                        authResponse.data.expiration);
                    CachedAuth = auth;
                    Logger.LogInformation("QINGLONG: Token obtained, expiration is {Expiration}", auth.ExpirationUnixSeconds);
                    return auth;
                }
                else if (authResponse.code == 200)
                {
                    Logger.LogError("QINGLONG: Token is missing or already expired");
                    throw new LoginException("Token is missing or already expired");
                }
                else if (authResponse.code == 400)
                {
                    Logger.LogError("QINGLONG: Wrong client ID or client secret");
                    throw new LoginException("Wrong client ID or client secret");
                }
                else
                {
                    Logger.LogError("QINGLONG: Log in response code {Code} message {Message}", authResponse.code, authResponse.message);
                    throw new LoginException($"Log in response code {authResponse.code} message {authResponse.message}");
                }
            }
            finally
            {
                LoginLock.Release();
            }
        }

        public async Task ExecuteCommandAsync(string command)
        {
            var auth = await GetAuthAsync();
            string cronName = Options.Value.Commands[command];

            using var findCronRequest = CreateAuthorizedRequest(
                HttpMethod.Get,
                QueryHelpers.AddQueryString("/open/crons", new Dictionary<string, string?>
                {
                    ["searchValue"] = cronName,
                }),
                auth);
            var findCronResponseMessage = await HttpClient.SendAsync(findCronRequest);
            QinglongCronModel? findCronResponse = await findCronResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (findCronResponse == null || findCronResponse.code != 200)
            {
                Logger.LogError("QINGLONG: Find cron task fail with code {Code}", findCronResponse?.code);
                throw new ExecutionException($"Find cron task fail with code {findCronResponse?.code}");
            }

            var id = findCronResponse?.data?.data?.FirstOrDefault(x => x.command == cronName)?.id;
            id ??= findCronResponse?.data?.data?.FirstOrDefault(x => x.name == cronName)?.id;
            if (id == null)
            {
                Logger.LogError("QINGLONG: No cron task found");
                throw new ExecutionException("No cron task found");
            }
            Logger.LogInformation("QINGLONG: Find cron task id {Id}", id);

            using var runCronRequest = CreateAuthorizedRequest(HttpMethod.Put, "/open/crons/run", auth, JsonContent.Create(new int[] { id.Value }));
            var runConResponseMessage = await HttpClient.SendAsync(runCronRequest);
            QinglongCronModel? runCronResponse = await runConResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (runCronResponse == null || runCronResponse.code != 200)
            {
                Logger.LogError("QINGLONG: Run cron task fail with code {Code}", runCronResponse?.code);
                throw new ExecutionException($"Run cron task fail with code {runCronResponse?.code}");
            }
            Logger.LogInformation("QINGLONG: Execute cron task id {Id} done", id);
        }

        public async Task RerunTodayTasks()
        {
            var auth = await GetAuthAsync();

            using var findCronRequest = CreateAuthorizedRequest(HttpMethod.Get, "/open/crons", auth);
            var findCronResponseMessage = await HttpClient.SendAsync(findCronRequest);
            QinglongCronModel? findCronResponse = await findCronResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (findCronResponse == null || findCronResponse.code != 200)
            {
                Logger.LogError("QINGLONG: Find cron task fail with code {Code}", findCronResponse?.code);
                throw new ExecutionException($"Find cron task fail with code {findCronResponse?.code}");
            }

            var cstNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Asia/Shanghai");
            Logger.LogInformation("China Standard Time Now is {CstNow}", cstNow);

            var cstTodayStart = new DateTimeOffset(cstNow.Year, cstNow.Month, cstNow.Day, 0, 0, 0, new TimeSpan(8, 0, 0));
            Logger.LogInformation("China Standard Time Today Start is {CstTodayStart}", cstTodayStart);

            var ids = findCronResponse?.data?.data?.Where(x => x.last_execution_time > cstTodayStart.ToUnixTimeSeconds() && x.last_execution_time < cstNow.ToUnixTimeSeconds()).Select(x => x.id).ToArray();
            if (ids == null)
            {
                Logger.LogError("QINGLONG: No cron task found");
                throw new ExecutionException("No cron task found");
            }
            Logger.LogInformation("QINGLONG: Find cron tasks with ids {Ids}", string.Join(",", ids));

            using var runCronRequest = CreateAuthorizedRequest(HttpMethod.Put, "/open/crons/run", auth, JsonContent.Create(ids));
            var runConResponseMessage = await HttpClient.SendAsync(runCronRequest);
            QinglongCronModel? runCronResponse = await runConResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (runCronResponse == null || runCronResponse.code != 200)
            {
                Logger.LogError("QINGLONG: Run cron task fail with code {Code}", runCronResponse?.code);
                throw new ExecutionException($"Run cron task fail with code {runCronResponse?.code}");
            }
            Logger.LogInformation("QINGLONG: Execute cron task id {Ids} done", string.Join(", ", ids));
        }
    }
}
