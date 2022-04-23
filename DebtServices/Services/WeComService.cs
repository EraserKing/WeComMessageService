using DebtServices.Processors;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using DebtServices.Processors.Interfaces;

namespace DebtServices.Services
{
    public class WeComService
    {
        private readonly ILogger<WeComService> Logger;
        private readonly IServiceProvider ServiceProvider;
        private readonly WeComServicesConfiguration WeComConfiguration;

        private Dictionary<ulong, WeComAccessToken> AccessTokenByAgent = new Dictionary<ulong, WeComAccessToken>();

        private static Dictionary<ulong, Type> Processors;
        private static object ProcessorInitializeLock = new object();

        public WeComService(ILogger<WeComService> logger, IServiceProvider serviceProvider, IOptions<WeComServicesConfiguration> weComConfiguration)
        {
            Logger = logger;
            ServiceProvider = serviceProvider;
            WeComConfiguration = weComConfiguration.Value;

            if (Processors == null)
            {
                Logger.LogInformation("WECOMSERVICE: Initialize processors...");
                lock (ProcessorInitializeLock)
                {
                    Processors = new Dictionary<ulong, Type>();
                    var serviceScope = serviceProvider.CreateScope();
                    foreach (var processor in serviceScope.ServiceProvider.GetServices<IProcessor>())
                    {
                        ulong processorAgentId = processor.GetProcessorAgentId();
                        Logger.LogInformation($"WECOMSERVICE: Initialize processor {processorAgentId}");
                        Processors[processorAgentId] = processor.GetType();
                    }
                }
            }
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
                string corpId = WeComConfiguration.AppConfigurations.First(x => x.AgentId == agentId).CorpId;
                string corpSecret = WeComConfiguration.AppConfigurations.First(x => x.AgentId == agentId).CorpSecret;
                var response = await client.GetStringAsync($"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={corpId}&corpsecret={corpSecret}");
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
            if (regularMessage == null)
            {
                Logger.LogInformation($"WECOMSERVICE: SEND_MESSAGE skipped - source is null");
                return;
            }
            HttpClient client = new HttpClient();
            var response = await client.PostAsync($"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={await GetAccessTokenAsync(regularMessage.AgentId)}", JsonContent.Create(regularMessage, regularMessage.GetType()));
            response.EnsureSuccessStatusCode();
            Logger.LogInformation($"WECOMSERVICE: SEND_MESSAGE {await response.Content.ReadAsStringAsync()}");
        }

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage)
        {
            if (!Processors.ContainsKey(receiveMessage.AgentID))
            {
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, "该应用未设置对应处理程序");
            }

            var serviceScope = ServiceProvider.CreateScope();

            var service = serviceScope.ServiceProvider.GetService(Processors[receiveMessage.AgentID]);
            if (service is IProcessor processor)
            {
                return await processor.ReplyMessageAsync(receiveMessage, this);
            }
            else
            {
                Logger.LogError($"WECOMSERVICE: No processor found for {receiveMessage.AgentID}");
                throw new ArgumentNullException(nameof(receiveMessage.AgentID));
            }
        }
    }
}
