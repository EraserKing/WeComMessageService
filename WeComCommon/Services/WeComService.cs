using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using WeComCommon.Models;
using WeComCommon.Models.Configurations;
using WeComCommon.Processors.Interfaces;

namespace WeComCommon.Services
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
                        Logger.LogInformation("WECOMSERVICE: Initialize processor {ProcessorAgentId}", processorAgentId);
                        Processors[processorAgentId] = processor.GetType();
                    }
                }
            }
        }

        private async Task<string> GetAccessTokenAsync(ulong agentId)
        {
            if (AccessTokenByAgent.ContainsKey(agentId) && DateTime.Now - AccessTokenByAgent[agentId].ObtainedDateTime < new TimeSpan(0, 0, AccessTokenByAgent[agentId].ExpiresIn))
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
                Logger.LogInformation("WECOMSERVICE: SEND_MESSAGE skipped - source is null");
                return;
            }
            HttpClient client = new HttpClient();
            var response = await client.PostAsJsonAsync($"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={await GetAccessTokenAsync(regularMessage.AgentId)}", regularMessage, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

            });
            response.EnsureSuccessStatusCode();
            Logger.LogInformation("WECOMSERVICE: SEND_MESSAGE {ResponseContent}", await response.Content.ReadAsStringAsync());
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
                try
                {
                    return await processor.ReplyMessageAsync(receiveMessage, this);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "WECOMSERVICE: Command failed for agent {AgentId} user {User} content {Content}", receiveMessage.AgentID, receiveMessage.FromUserName, receiveMessage.Content);
                    return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, FormatExceptionForUser(ex));
                }
            }
            else
            {
                Logger.LogError("WECOMSERVICE: No processor found for {AgentID}", receiveMessage.AgentID);
                throw new ArgumentNullException(nameof(receiveMessage.AgentID));
            }
        }

        public void RunInBackground(WeComReceiveMessage receiveMessage, Func<Task> work)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await work();
                }
                catch (Exception ex)
                {
                    await ReportFailureAsync(receiveMessage.AgentID, receiveMessage.FromUserName, ex, $"agent {receiveMessage.AgentID} user {receiveMessage.FromUserName} content {receiveMessage.Content}");
                }
            });
        }

        public async Task ReportFailureAsync(ulong agentId, string toUser, Exception exception, string context)
        {
            Logger.LogError(exception, "WECOMSERVICE: {Context} failed", context);
            if (string.IsNullOrWhiteSpace(toUser))
            {
                return;
            }

            try
            {
                await SendMessageAsync(WeComRegularMessage.CreateTextMessage(agentId, toUser, FormatExceptionForUser(exception)));
            }
            catch (Exception sendEx)
            {
                Logger.LogError(sendEx, "WECOMSERVICE: Failed to send failure reply for {Context}", context);
            }
        }

        public static string FormatExceptionForUser(Exception exception)
        {
            var messages = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) && (messages.Count == 0 || messages[^1] != current.Message))
                {
                    messages.Add(current.Message);
                }
            }

            var text = messages.Count == 0 ? exception.ToString() : string.Join(" -> ", messages);
            const int maxLength = 1800;
            return text.Length <= maxLength ? text : text[..maxLength];
        }
    }
}
