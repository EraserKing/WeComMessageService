using DebtServices.Database;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;

namespace DebtServices.Services
{
    public class NotificationService : IHostedService, IDisposable
    {
        private readonly ILogger<NotificationService> Logger;
        private readonly IServiceProvider ServiceProvider;

        private Timer NewReleaseTimer;
        private Timer NewListingTimer;

        public NotificationService(IServiceProvider serviceProvider, ILogger<NotificationService> logger)
        {
            ServiceProvider = serviceProvider;
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

            using var servicesScope = ServiceProvider.CreateScope();
            var debtServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<DebtServiceConfiguration>>().Value;

            NewReleaseTimer = new Timer(async (param) =>
            {
                using var timerServicesScope = ServiceProvider.CreateScope();
                Logger.LogInformation("NOTIFICATION: Start release routine");
                var newReleases = await CheckNewReleasesAsync();
                await timerServicesScope.ServiceProvider.GetRequiredService<WeComService>().SendMessageAsync(newReleases);
                Logger.LogInformation("NOTIFICATION: Finish release routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(debtServiceConfiguration.NewReleaseCheckHour, debtServiceConfiguration.NewReleaseCheckMinute), TimeSpan.FromDays(1));
            Logger.LogInformation($"NOTIFICATION: New release timer created at UTC {debtServiceConfiguration.NewReleaseCheckHour}:{debtServiceConfiguration.NewReleaseCheckMinute}");
        }

        public void CreateListingTimer()
        {
            if (NewListingTimer != null)
            {
                Logger.LogInformation("NOTIFICATION: Current new listing timer is not null. Dispose and re-create.");
                NewListingTimer.Change(Timeout.Infinite, 0);
                NewListingTimer.Dispose();
            }

            using var servicesScope = ServiceProvider.CreateScope();
            var debtServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<DebtServiceConfiguration>>().Value;

            NewListingTimer = new Timer(async (param) =>
            {
                using var timerServicesScope = ServiceProvider.CreateScope();
                Logger.LogInformation("NOTIFICATION: Start listing routine");
                var newListings = await CheckNewListingsAsync();
                foreach (var newListing in newListings)
                {
                    await timerServicesScope.ServiceProvider.GetRequiredService<WeComService>().SendMessageAsync(newListing);
                }
                Logger.LogInformation("NOTIFICATION: Finish listing routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(debtServiceConfiguration.NewListingCheckHour, debtServiceConfiguration.NewListingCheckMinute), TimeSpan.FromDays(1));
            Logger.LogInformation($"NOTIFICATION: New listing timer created at UTC {debtServiceConfiguration.NewListingCheckHour}:{debtServiceConfiguration.NewListingCheckMinute}");
        }

        public async Task<IList<WeComRegularMessage>> CheckNewListingsAsync()
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var eastmoneyService = servicesScope.ServiceProvider.GetRequiredService<EastmoneyService>();
            var cosmosDbService = servicesScope.ServiceProvider.GetRequiredService<CosmosDbService<DebtReminderContext, DebtReminderModel>>();
            var debtServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<DebtServiceConfiguration>>().Value;

            var newListings = await eastmoneyService.GetNewListingAsync();
            if (newListings == null || newListings.Length == 0)
            {
                Logger.LogInformation("NOTIFICATION: No new listings today");
                return null;
            }

            var newDebtCodes = newListings.Select(x => x.SECURITY_CODE).ToArray();
            Logger.LogInformation($"NOTIFICATION: New codes on listing today: {string.Join(" ", newDebtCodes)}");
            (var operationResult, var subscriptionResults) = await cosmosDbService.QueryItemsAsync(x => x.ReminderType == ReminderType.LISTING && Array.Exists(newDebtCodes, debtCode => debtCode == x.DebtCode));

            if (operationResult == CosmosDbActionResult.Failed || subscriptionResults == null)
            {
                Logger.LogError("NOTIFICATION: Failed to get subscribers for new listings");
                return null;
            }

            List<WeComRegularMessage> messages = new List<WeComRegularMessage>();

            foreach (var subscriptionsByDebtCode in subscriptionResults.GroupBy(x => x.DebtCode))
            {
                var newListing = newListings.FirstOrDefault(x => x.SECURITY_CODE == subscriptionsByDebtCode.Key);
                if (newListing == null)
                {
                    Logger.LogError($"NOTIFICATION: Debt {subscriptionsByDebtCode.Key} is not listed today while it exists in data");
                    continue;
                }

                string userIds = string.Join("|", subscriptionsByDebtCode.Select(x => x.UserName));
                if (string.IsNullOrWhiteSpace(userIds))
                {
                    Logger.LogError($"NOTIFICATION: No users subscribed {subscriptionsByDebtCode.FirstOrDefault()?.DebtCode} / {subscriptionsByDebtCode.FirstOrDefault()?.DebtName} ");
                    continue;
                }

                messages.Add(WeComRegularMessage.CreateTextCardMessage(
                    debtServiceConfiguration.SendByAgentId,
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

        public async Task<WeComRegularMessage> CheckNewReleasesAsync()
        {
            using var servicesScope = ServiceProvider.CreateScope();
            var eastmoneyService = servicesScope.ServiceProvider.GetRequiredService<EastmoneyService>();
            var cosmosDbService = servicesScope.ServiceProvider.GetRequiredService<CosmosDbService<DebtReminderContext, DebtReminderModel>>();
            var debtServiceConfiguration = servicesScope.ServiceProvider.GetRequiredService<IOptions<DebtServiceConfiguration>>().Value;

            var newReleases = await eastmoneyService.GetNewReleasesAsync();
            if (newReleases == null || newReleases.Length == 0)
            {
                return null;
            }

            (var operationResult, var subscriptionResults) = await cosmosDbService.QueryItemsAsync(x => x.ReminderType == ReminderType.RELEASE);
            if (operationResult == CosmosDbActionResult.Failed || subscriptionResults == null)
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
                    debtServiceConfiguration.SendByAgentId,
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
