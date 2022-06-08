using DebtServices.Exceptions.Qinglong;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;
using OtpNet;
using System.Text;

namespace DebtServices.Services
{
    public class QinglongService
    {
        private readonly ILogger<QinglongService> Logger;
        private readonly IServiceProvider ServiceProvider;

        private static HttpClient HttpClient { get; set; }
        private static string Token { get; set; } = null;

        public QinglongService(IServiceProvider serviceProvider, ILogger<QinglongService> logger)
        {
            ServiceProvider = serviceProvider;
            Logger = logger;
        }

        public bool IsCommandValid(string command)
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var qinglongServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<QinglongServiceConfiguration>>().Value;

            return qinglongServiceConfiguration.Commands?.ContainsKey(command) ?? false;
        }

        private static long GetUnixEpochMs() => (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;

        public async Task LoginAsync()
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var qinglongServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<QinglongServiceConfiguration>>().Value;

            if (string.IsNullOrEmpty(qinglongServiceConfiguration.SiteUrl) || string.IsNullOrEmpty(qinglongServiceConfiguration.UserName) || string.IsNullOrEmpty(qinglongServiceConfiguration.Password))
            {
                Logger.LogError("QINGLONG: Site, user name, or password is not set");
                throw new LoginException("Site, user name, or password is not set");
            }

            HttpClient = new HttpClient();
            Token = string.Empty;

            Logger.LogInformation("QINGLONG: Ready to log in");
            var loginResponseMessage = await HttpClient.PostAsJsonAsync($"{qinglongServiceConfiguration.SiteUrl}/api/user/login?t={GetUnixEpochMs()}", new
            {
                username = qinglongServiceConfiguration.UserName,
                password = qinglongServiceConfiguration.Password
            });
            QinglongLoginOrUserModel? loginResponse = await loginResponseMessage.Content.ReadFromJsonAsync<QinglongLoginOrUserModel>();
            if (loginResponse == null)
            {
                Logger.LogError("QINGLONG: Log in response is null");
                throw new LoginException("Log in response is null");
            }
            else if (loginResponse.code == 200)
            {
                Logger.LogInformation("QINGLONG: Token obtained");
                Token = loginResponse.data.token;
                HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token}");
            }
            else if (loginResponse.code == 400)
            {
                Logger.LogError("QINGLONG: Wrong user name or password");
                throw new LoginException("Wrong user name or password");
            }
            else if (loginResponse.code == 420)
            {
                Logger.LogInformation("QINGLONG: 2FA needed");
                if (string.IsNullOrEmpty(qinglongServiceConfiguration.TotpKey))
                {
                    Logger.LogError("QINGLONG: 2FA key is not set");
                    throw new LoginException("2FA key is not set");
                }
                else
                {
                    var login2FaResponseMessage = await HttpClient.PutAsJsonAsync($"{qinglongServiceConfiguration.SiteUrl}/api/user/two-factor/login?t={GetUnixEpochMs()}", new
                    {
                        username = qinglongServiceConfiguration.UserName,
                        password = qinglongServiceConfiguration.Password,
                        code = new Totp(Base32Encoding.ToBytes(qinglongServiceConfiguration.TotpKey)).ComputeTotp()

                    });

                    QinglongLoginOrUserModel? login2faResponse = await login2FaResponseMessage.Content.ReadFromJsonAsync<QinglongLoginOrUserModel>();
                    if (login2faResponse == null)
                    {
                        Logger.LogError("QINGLONG: Log in 2FA response is null");
                        throw new LoginException("Log in 2FA response is null");
                    }
                    else if (login2faResponse.code == 430)
                    {
                        Logger.LogError("QINGLONG: Log in 2FA code is wrong");
                        throw new LoginException("Log in 2FA code is wrong");
                    }
                    else if (login2faResponse.code == 200)
                    {
                        Logger.LogInformation("QINGLONG: Token obtained");
                        Token = login2faResponse.data.token;
                        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token}");
                    }
                    else
                    {
                        Logger.LogError($"QINGLONG: Log in 2FA response code {login2faResponse.code} message {login2faResponse.message}");
                        throw new LoginException($"Log in 2FA response code {login2faResponse.code} message {login2faResponse.message}");
                    }
                }
            }
            else
            {
                Logger.LogError($"QINGLONG: Log in response code {loginResponse.code} message {loginResponse.message}");
                throw new LoginException($"Log in response code {loginResponse.code} message {loginResponse.message}");
            }
        }

        public async Task ValidateTokenAsync()
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var qinglongServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<QinglongServiceConfiguration>>().Value;
            if (string.IsNullOrEmpty(qinglongServiceConfiguration.SiteUrl))
            {
                Logger.LogError("QINGLONG: Site is not set");
                throw new LoginException("Site is not set");
            }

            if (Token == null)
            {
                Logger.LogInformation("QINGLONG: Current token is null");
                await LoginAsync();
            }

            var getUserResponseMessage = await HttpClient.GetAsync($"{qinglongServiceConfiguration.SiteUrl}/api/user?t={GetUnixEpochMs()}");
            QinglongLoginOrUserModel? getUserResponse = await getUserResponseMessage.Content.ReadFromJsonAsync<QinglongLoginOrUserModel>();
            if (getUserResponse == null || getUserResponse.code != 200)
            {
                Logger.LogInformation("QINGLONG: Need re-log in");
                await LoginAsync();
            }
        }

        public async Task ExecuteCommandAsync(string command)
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var qinglongServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<QinglongServiceConfiguration>>().Value;

            await ValidateTokenAsync();
            string cronName = qinglongServiceConfiguration.Commands[command];

            var findCronResponseMessage = await HttpClient.GetAsync($"{qinglongServiceConfiguration.SiteUrl}/api/crons?searchValue={cronName}&t={GetUnixEpochMs()}");
            QinglongCronModel? findCronResponse = await findCronResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (findCronResponse == null || findCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Find cron task fail with code {findCronResponse?.code}");
                throw new ExecutionException($"Find cron task fail with code {findCronResponse?.code}");
            }

            var id = findCronResponse?.data?.FirstOrDefault(x => x.name == cronName)?.id;
            if (id == null)
            {
                Logger.LogError("QINGLONG: No cron task found");
                throw new ExecutionException("No cron task found");
            }
            Logger.LogInformation($"QINGLONG: Find cron task id {id}");

            var runConResponseMessage = await HttpClient.PutAsJsonAsync($"{qinglongServiceConfiguration.SiteUrl}/api/crons/run?t={GetUnixEpochMs()}", new int[] { id.Value });
            QinglongCronModel? runCronResponse = await runConResponseMessage.Content.ReadFromJsonAsync<QinglongCronModel>();
            if (runCronResponse == null || runCronResponse.code != 200)
            {
                Logger.LogError($"QINGLONG: Run cron task fail with code {runCronResponse?.code}");
                throw new ExecutionException($"Run cron task fail with code {runCronResponse?.code}");
            }
            Logger.LogInformation($"QINGLONG: Execute cron task id {id} done");
        }
    }
}
