using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;

namespace DebtServices.Services
{
    public class NotificationService : IHostedService, IDisposable
    {
        private readonly ILogger<NotificationService> Logger;
        private readonly IServiceProvider Services;

        private Timer NewReleaseTimer;
        private Timer NewListingTimer;

        public NotificationService(IServiceProvider services, ILogger<NotificationService> logger)
        {
            Services = services;
            Logger = logger;
        }

        private TimeSpan GetTimeSpanFromNextUtcHourMinute(int hour, int minute)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcCheckReleaseTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, hour, minute, 0, DateTimeKind.Utc);

            TimeSpan startupDelay = ((utcNow < utcCheckReleaseTime) ? utcCheckReleaseTime : utcCheckReleaseTime.AddDays(1)) - utcNow;

            return startupDelay;
        }

        public void CreateReleaseTimer()
        {
            if (NewReleaseTimer != null)
            {
                Logger.LogInformation("NOTIFICATION: Current new release timer is not null. Dispose and re-create.");
                NewReleaseTimer.Change(Timeout.Infinite, 0);
                NewReleaseTimer.Dispose();
            }

            using var servicesScope = Services.CreateScope();
            var weComConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<WeComConfiguration>>().Value;

            NewReleaseTimer = new Timer(async (param) =>
            {
                using var timerServicesScope = Services.CreateScope();
                Logger.LogInformation("NOTIFICATION: Start release routine");
                var newReleases = await CheckNewReleasesAsync();
                await timerServicesScope.ServiceProvider.GetRequiredService<WeComService>().SendMessageAsync(newReleases);
                Logger.LogInformation("NOTIFICATION: Finish release routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(weComConfiguration.NewReleaseCheckHour, weComConfiguration.NewReleaseCheckMinute), TimeSpan.FromDays(1));
            Logger.LogInformation($"NOTIFICATION: New release timer created at UTC {weComConfiguration.NewReleaseCheckHour}:{weComConfiguration.NewReleaseCheckMinute}");
        }

        public void CreateListingTimer()
        {
            if (NewListingTimer != null)
            {
                Logger.LogInformation("NOTIFICATION: Current new listing timer is not null. Dispose and re-create.");
                NewListingTimer.Change(Timeout.Infinite, 0);
                NewListingTimer.Dispose();
            }

            using var servicesScope = Services.CreateScope();
            var weComConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<WeComConfiguration>>().Value;

            NewListingTimer = new Timer(async (param) =>
            {
                using var timerServicesScope = Services.CreateScope();
                Logger.LogInformation("NOTIFICATION: Start listing routine");
                var newListings = await CheckNewListingsAsync();
                foreach (var newListing in newListings)
                {
                    await timerServicesScope.ServiceProvider.GetRequiredService<WeComService>().SendMessageAsync(newListing);
                }
                Logger.LogInformation("NOTIFICATION: Finish listing routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(weComConfiguration.NewListingCheckHour, weComConfiguration.NewListingCheckMinute), TimeSpan.FromDays(1));
            Logger.LogInformation($"NOTIFICATION: New listing timer created at UTC {weComConfiguration.NewListingCheckHour}:{weComConfiguration.NewListingCheckMinute}");
        }

        public async Task<IList<WeComRegularMessage>> CheckNewListingsAsync(string userName = null)
        {
            using var servicesScope = Services.CreateScope();
            var eastmoneyService = servicesScope.ServiceProvider.GetRequiredService<EastmoneyService>();
            var cosmosDbService = servicesScope.ServiceProvider.GetRequiredService<CosmosDbService>();
            var weComConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<WeComConfiguration>>().Value;

            var newListings = await eastmoneyService.GetNewListingAsync();
            if (newListings == null || newListings.Length == 0)
            {
                Logger.LogInformation("NOTIFICATION: No new listings today");
                return null;
            }

            (var operationResult, var subscriptionResults) = await cosmosDbService.QueryItemsAsync(userName, ReminderType.LISTING, null);
            if (operationResult == CosmosDbService.DbActionResult.Failed || subscriptionResults == null)
            {
                Logger.LogError("NOTIFICATION: Failed to get subscribers for new listings");
                return null;
            }

            List<WeComRegularMessage> messages = new List<WeComRegularMessage>();

            foreach (var newListing in newListings)
            {
                string userIds = string.Join("|", subscriptionResults.Where(x => x.DebtCode == newListing.SECURITY_CODE).Select(x => x.UserName));
                if (string.IsNullOrWhiteSpace(userIds))
                {
                    Logger.LogError($"NOTIFICATION: No users subscribed {newListing.SECURITY_CODE} / {newListing.SECURITY_NAME_ABBR} ");
                    continue;
                }

                messages.Add(WeComRegularMessage.CreateTextCardMessage(
                    weComConfiguration.AgentId,
                    userIds,
                    "新上市",
                    newListing.MakeCardContent(),
                    "https://data.eastmoney.com/kzz/default.html",
                    "查看列表"
                    ));
                Logger.LogInformation($"NOTIFICATION: Collect {newListing.SECURITY_CODE} / {newListing.SECURITY_NAME_ABBR} to {userIds}");
            }
            return messages;
        }

        public async Task<WeComRegularMessage> CheckNewReleasesAsync(string userName = null)
        {
            using var servicesScope = Services.CreateScope();
            var eastmoneyService = servicesScope.ServiceProvider.GetRequiredService<EastmoneyService>();
            var cosmosDbService = servicesScope.ServiceProvider.GetRequiredService<CosmosDbService>();
            var weComConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<WeComConfiguration>>().Value;

            var newReleases = await eastmoneyService.GetNewReleasesAsync();
            if (newReleases == null || newReleases.Length == 0)
            {
                return null;
            }

            (var operationResult, var subscriptionResults) = await cosmosDbService.QueryItemsAsync(userName, ReminderType.RELEASE, null);
            if (operationResult == CosmosDbService.DbActionResult.Failed || subscriptionResults == null)
            {
                Logger.LogError("NOTIFICATION: No new releases today");
                return null;
            }

            string userIds = string.Join("|", subscriptionResults.Where(x => x.DebtCode == "@all").Select(x => x.UserName));
            if (string.IsNullOrWhiteSpace(userIds))
            {
                Logger.LogError("NOTIFICATION: No users subscribed");
                return null;
            }

            string cardContents = string.Join($"{Environment.NewLine}{Environment.NewLine}", newReleases.Select(x => x.MakeCardContent()));

            Logger.LogInformation($"NOTIFICATION: Collect {newReleases.Length} release to {userIds}");

            return WeComRegularMessage.CreateTextCardMessage(
                    weComConfiguration.AgentId,
                    userIds,
                    "新申购",
                    cardContents,
                    "https://data.eastmoney.com/kzz/default.html",
                    "查看列表"
            );
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CreateListingTimer();
            CreateReleaseTimer();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            NewReleaseTimer.Change(Timeout.Infinite, 0);
            NewListingTimer.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            NewReleaseTimer?.Dispose();
            NewListingTimer?.Dispose();
        }
    }
}
