using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qinglong.Exceptions;
using Qinglong.Models;
using Qinglong.Models.Configurations;
using System.Net.Http.Json;

namespace Qinglong.Services
{
    public class QinglongService
    {
        private readonly ILogger<QinglongService> Logger;
        private readonly IOptions<QinglongServiceConfiguration> Options;

        private static HttpClient HttpClient { get; set; } = new HttpClient();
        private static int TokenExpiration { get; set; }

        public QinglongService(ILogger<QinglongService> logger, IOptions<QinglongServiceConfiguration> options)
        {
            Logger = logger;
            Options = options;
            if (HttpClient.BaseAddress == null)
            {
                HttpClient.BaseAddress = new Uri(options.Value.SiteUrl);
            }
        }

        public bool IsCommandValid(string command) => Options.Value.Commands?.ContainsKey(command) ?? false;

        private static long GetUnixEpochSeconds() => (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;

        public async Task LoginAsync()
        {
            if (string.IsNullOrEmpty(Options.Value.SiteUrl) || string.IsNullOrEmpty(Options.Value.ClientId) || string.IsNullOrEmpty(Options.Value.ClientSecret))
            {
                Logger.LogError("QINGLONG: Site, client ID, or client password is not set");
                throw new LoginException("Site, client ID, or client password is not set");
            }

            if (TokenExpiration > GetUnixEpochSeconds() + 60 * 60)
            {
                Logger.LogInformation("QINGLONG: Token is still valid");
                return;
            }

            HttpClient.DefaultRequestHeaders.Remove("Authorization");
            Logger.LogInformation("QINGLONG: Ready to log in");

            var authMessage = await HttpClient.GetAsync(QueryHelpers.AddQueryString("/open/auth/token", new Dictionary<string, string?>
            {
                ["client_id"] = Options.Value.ClientId,
                ["client_secret"] = Options.Value.ClientSecret,
            }));
            QinglongAuthTokenModel? authResponse = await authMessage.Content.ReadFromJsonAsync<QinglongAuthTokenModel>();
            if (authResponse == null)
            {
                Logger.LogError("QINGLONG: Log in response is null");
                throw new LoginException("Log in response is null");
            }
            else if (authResponse.code == 200)
            {
                Logger.LogInformation($"QINGLONG: Token obtained, expiration is {authResponse.data.expiration}");
                HttpClient.DefaultRequestHeaders.Add("Authorization", $"{authResponse.data.token_type} {authResponse.data.token}");
                TokenExpiration = authResponse.data.expiration;
            }
            else if (authResponse.code == 400)
            {
                Logger.LogError("QINGLONG: Wrong client ID or client secret");
                throw new LoginException("Wrong client ID or client secret");
            }
            else
            {
                Logger.LogError($"QINGLONG: Log in response code {authResponse.code} message {authResponse.message}");
                throw new LoginException($"Log in response code {authResponse.code} message {authResponse.message}");
            }
        }

        public async Task ExecuteCommandAsync(string command)
        {
            await LoginAsync();
            string cronName = Options.Value.Commands[command];

            var findCronResponseMessage = await HttpClient.GetAsync(QueryHelpers.AddQueryString("/open/crons", new Dictionary<string, string?>
            {
                ["seachValue"] = cronName,
            }));
            QinglongCronModel? findCronResponse = await findCronResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (findCronResponse == null || findCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Find cron task fail with code {findCronResponse?.code}");
                throw new ExecutionException($"Find cron task fail with code {findCronResponse?.code}");
            }

            var id = findCronResponse?.data?.data?.FirstOrDefault(x => x.command == cronName)?.id;
            id ??= findCronResponse?.data?.data?.FirstOrDefault(x => x.name == cronName)?.id;
            if (id == null)
            {
                Logger.LogError("QINGLONG: No cron task found");
                throw new ExecutionException("No cron task found");
            }
            Logger.LogInformation($"QINGLONG: Find cron task id {id}");

            var runConResponseMessage = await HttpClient.PutAsJsonAsync("/open/crons/run", new int[] { id.Value });
            QinglongCronModel? runCronResponse = await runConResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (runCronResponse == null || runCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Run cron task fail with code {runCronResponse?.code}");
                throw new ExecutionException($"Run cron task fail with code {runCronResponse?.code}");
            }
            Logger.LogInformation($"QINGLONG: Execute cron task id {id} done");
        }

        public async Task RerunTodayTasks()
        {
            await LoginAsync();

            var findCronResponseMessage = await HttpClient.GetAsync("/open/crons");
            QinglongCronModel? findCronResponse = await findCronResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (findCronResponse == null || findCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Find cron task fail with code {findCronResponse?.code}");
                throw new ExecutionException($"Find cron task fail with code {findCronResponse?.code}");
            }

            var cstNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now, "Asia/Shanghai");
            Logger.LogInformation($"China Standard Time Now is {cstNow}");

            var cstTodayStart = new DateTimeOffset(cstNow.Year, cstNow.Month, cstNow.Day, 0, 0, 0, new TimeSpan(8, 0, 0));
            Logger.LogInformation($"China Standard Time Today Start is {cstTodayStart}");

            var ids = findCronResponse?.data?.data?.Where(x => x.last_execution_time > cstTodayStart.ToUnixTimeSeconds() && x.last_execution_time < cstNow.ToUnixTimeSeconds()).Select(x => x.id).ToArray();
            if (ids == null)
            {
                Logger.LogError("QINGLONG: No cron task found");
                throw new ExecutionException("No cron task found");
            }
            Logger.LogInformation($"QINGLONG: Find cron tasks with ids {string.Join(",", ids)}");

            var runConResponseMessage = await HttpClient.PutAsJsonAsync("/open/crons/run", ids);
            QinglongCronModel? runCronResponse = await runConResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (runCronResponse == null || runCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Run cron task fail with code {runCronResponse?.code}");
                throw new ExecutionException($"Run cron task fail with code {runCronResponse?.code}");
            }
            Logger.LogInformation($"QINGLONG: Execute cron task id {string.Join(", ", ids)} done");
        }
    }
}
