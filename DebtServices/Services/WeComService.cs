using DebtServices.MessageProcessors;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DebtServices.Services
{
    public class WeComService
    {
        private readonly ILogger<WeComService> Logger;
        private readonly WeComConfiguration WeComConfiguration;
        private readonly DebtSubscriptionService SubscriptionService;
        private readonly NotificationService NotificationService;

        private Dictionary<ulong, WeComAccessToken> AccessTokenByAgent = new Dictionary<ulong, WeComAccessToken>();

        private readonly DebtMessageProcessor DebtMessageProcessor;

        public WeComService(ILogger<WeComService> logger, IOptions<WeComConfiguration> weComConfiguration, DebtSubscriptionService subscriptionService, NotificationService notificationService)
        {
            Logger = logger;
            WeComConfiguration = weComConfiguration.Value;
            SubscriptionService = subscriptionService;
            NotificationService = notificationService;

            DebtMessageProcessor = new DebtMessageProcessor();

            notificationService.CreateReleaseTimer(SendMessageAsync);
            notificationService.CreateListingTimer(SendMessageAsync);
        }

        private async Task<string> GetAccessTokenAsync(ulong agentId)
        {
            if (AccessTokenByAgent.ContainsKey(agentId) && (DateTime.Now - AccessTokenByAgent[agentId].ObtainedDateTime) < new TimeSpan(0, 0, AccessTokenByAgent[agentId].ExpiresIn))
            {
                return AccessTokenByAgent[agentId].AccessToken;
            }
            else
            {
                Logger.LogDebug("WECOMSERVICE: RECLAIM ACCESS TOKEN");
                HttpClient client = new HttpClient();
                var response = await client.GetStringAsync($"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={WeComConfiguration.CorpId}&corpsecret={WeComConfiguration.CorpSecret}");
                WeComAccessToken newToken = JsonSerializer.Deserialize<WeComAccessToken>(response);
                if (newToken != null)
                {
                    AccessTokenByAgent[agentId] = newToken;
                    return newToken.AccessToken;
                }
                throw new InvalidOperationException($"Unable to gain access token for app {agentId}");
            }
        }

        public async Task SendMessageAsync(WeComRegularMessage regularMessage)
        {
            HttpClient client = new HttpClient();
            var response = await client.PostAsync($"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={await GetAccessTokenAsync(WeComConfiguration.AgentId)}", JsonContent.Create(regularMessage, regularMessage.GetType()));
            response.EnsureSuccessStatusCode();
            Logger.LogInformation($"WECOMSERVICE: SEND_MESSAGE {await response.Content.ReadAsStringAsync()}");
        }

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage)
        {
            switch (receiveMessage.AgentID)
            {
                case 1000003:
                    return await DebtMessageProcessor.ReplyMessageAsync(receiveMessage, SubscriptionService, this, NotificationService);

                default:
                    return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, "该应用未设置对应处理程序");
            }
        }
    }
}
