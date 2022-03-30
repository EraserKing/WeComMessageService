using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;

namespace DebtServices.Services
{
    public class NotificationService
    {
        private readonly ILogger<NotificationService> Logger;
        private readonly WeComConfiguration WeComConfiguration;
        private readonly EastmoneyService EastmoneyService;
        private readonly CosmosDbService CosmosDbService;

        private Timer NewReleaseTimer;
        private Timer NewListingTimer;

        public NotificationService(ILogger<NotificationService> logger, IOptions<WeComConfiguration> weComConfiguration,
            EastmoneyService eastmoneyService, CosmosDbService cosmosDbService)
        {
            Logger = logger;
            WeComConfiguration = weComConfiguration.Value;
            EastmoneyService = eastmoneyService;
            CosmosDbService = cosmosDbService;
        }

        private TimeSpan GetTimeSpanFromNextUtcHourMinute(int hour, int minute)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcCheckReleaseTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, hour, minute, 0, DateTimeKind.Utc);

            TimeSpan startupDelay = ((utcNow < utcCheckReleaseTime) ? utcCheckReleaseTime : utcCheckReleaseTime.AddDays(1)) - utcNow;

            return startupDelay;
        }

        public void CreateReleaseTimer(Func<WeComRegularMessage, Task> sendMessageAction)
        {
            if (NewReleaseTimer != null)
            {
                NewReleaseTimer.Dispose();
            }

            NewReleaseTimer = new Timer(async (param) =>
            {
                Logger.LogInformation("NOTIFICATION: Start release routine");
                var newReleases = await CheckNewReleasesAsync();
                await sendMessageAction(newReleases);
                Logger.LogInformation("NOTIFICATION: Finish release routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(WeComConfiguration.NewReleaseCheckHour, WeComConfiguration.NewReleaseCheckMinute), TimeSpan.FromDays(1));
        }

        public void CreateListingTimer(Func<WeComRegularMessage, Task> sendMessageAction)
        {
            if (NewListingTimer != null)
            {
                NewListingTimer.Dispose();
            }

            NewListingTimer = new Timer(async (param) =>
            {
                Logger.LogInformation("NOTIFICATION: Start listing routine");
                var newReleases = await CheckNewListingsAsync();
                foreach(var newRelease in newReleases)
                {
                    await sendMessageAction(newRelease);
                }
                Logger.LogInformation("NOTIFICATION: Finish listing routine");
            }, null, GetTimeSpanFromNextUtcHourMinute(WeComConfiguration.NewListingCheckHour, WeComConfiguration.NewListingCheckMinute), TimeSpan.FromDays(1));
        }

        public async Task<IList<WeComRegularMessage>> CheckNewListingsAsync(string userName = null)
        {
            var newListings = await EastmoneyService.GetNewListingAsync();
            if (newListings == null || newListings.Length == 0)
            {
                Logger.LogInformation("NOTIFICATION: No new listings today");
                return null;
            }

            (var operationResult, var subscriptionResults) = await CosmosDbService.QueryItemsAsync(userName, ReminderType.LISTING, null);
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
                    WeComConfiguration.AgentId,
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
            var newReleases = await EastmoneyService.GetNewReleasesAsync();
            if (newReleases == null || newReleases.Length == 0)
            {
                return null;
            }

            (var operationResult, var subscriptionResults) = await CosmosDbService.QueryItemsAsync(userName, ReminderType.RELEASE, null);
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
                    WeComConfiguration.AgentId,
                    userIds,
                    "新申购",
                    cardContents,
                    "https://data.eastmoney.com/kzz/default.html",
                    "查看列表"
            );
        }
    }
}
