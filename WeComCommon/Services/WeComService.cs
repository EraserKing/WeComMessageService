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

        private readonly HttpClient HttpClient = new();
        private readonly SemaphoreSlim TokenLock = new(1, 1);
        private readonly Dictionary<ulong, WeComAccessToken> AccessTokenByAgent = new();
        private static readonly AsyncLocal<AsyncServiceScope?> PendingWorkScope = new();
        private static readonly JsonSerializerOptions MessageJsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

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
                    if (Processors == null)
                    {
                        var processors = new Dictionary<ulong, Type>();
                        using var serviceScope = serviceProvider.CreateScope();
                        foreach (var processor in serviceScope.ServiceProvider.GetServices<IProcessor>())
                        {
                            ulong processorAgentId = processor.GetProcessorAgentId();
                            Logger.LogInformation("WECOMSERVICE: Initialize processor {ProcessorAgentId}", processorAgentId);
                            processors[processorAgentId] = processor.GetType();
                        }
                        Processors = processors;
                    }
                }
            }
        }

        private async Task<string> GetAccessTokenAsync(ulong agentId)
        {
            await TokenLock.WaitAsync();
            try
            {
                if (AccessTokenByAgent.TryGetValue(agentId, out var cached)
                    && !string.IsNullOrEmpty(cached.AccessToken)
                    && cached.ObtainedDateTime.AddSeconds(Math.Max(0, cached.ExpiresIn - 60)) > DateTimeOffset.UtcNow)
                {
                    return cached.AccessToken;
                }

                Logger.LogDebug("WECOMSERVICE: RECLAIM ACCESS TOKEN");
                string corpId = WeComConfiguration.AppConfigurations.First(x => x.AgentId == agentId).CorpId;
                string corpSecret = WeComConfiguration.AppConfigurations.First(x => x.AgentId == agentId).CorpSecret;
                var response = await HttpClient.GetStringAsync($"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={corpId}&corpsecret={corpSecret}");
                WeComAccessToken newToken = JsonSerializer.Deserialize<WeComAccessToken>(response);
                if (newToken == null || newToken.ErrCode != 0 || string.IsNullOrEmpty(newToken.AccessToken))
                {
                    throw new InvalidOperationException($"Unable to gain access token for app {agentId}: {newToken?.ErrCode} {newToken?.ErrMsg}");
                }

                newToken.ObtainedDateTime = DateTimeOffset.UtcNow;
                AccessTokenByAgent[agentId] = newToken;
                return newToken.AccessToken;
            }
            finally
            {
                TokenLock.Release();
            }
        }

        public async Task SendMessageAsync(WeComRegularMessage regularMessage)
        {
            if (regularMessage == null)
            {
                Logger.LogInformation("WECOMSERVICE: SEND_MESSAGE skipped - source is null");
                return;
            }
            var response = await HttpClient.PostAsJsonAsync($"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={await GetAccessTokenAsync(regularMessage.AgentId)}", regularMessage, MessageJsonOptions);
            response.EnsureSuccessStatusCode();
            Logger.LogInformation("WECOMSERVICE: SEND_MESSAGE {ResponseContent}", await response.Content.ReadAsStringAsync());
        }

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage)
        {
            if (!Processors.ContainsKey(receiveMessage.AgentID))
            {
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, "该应用未设置对应处理程序");
            }

            var serviceScope = ServiceProvider.CreateAsyncScope();
            PendingWorkScope.Value = serviceScope;
            try
            {
                var service = serviceScope.ServiceProvider.GetService(Processors[receiveMessage.AgentID]);
                if (service is not IProcessor processor)
                {
                    Logger.LogError("WECOMSERVICE: No processor found for {AgentID}", receiveMessage.AgentID);
                    throw new ArgumentNullException(nameof(receiveMessage.AgentID));
                }

                return await processor.ReplyMessageAsync(receiveMessage, this);
            }
            catch (Exception ex) when (ex is not ArgumentNullException)
            {
                Logger.LogError(ex, "WECOMSERVICE: Command failed for agent {AgentId} user {User} content {Content}", receiveMessage.AgentID, receiveMessage.FromUserName, receiveMessage.Content);
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, FormatExceptionForUser(ex));
            }
            finally
            {
                if (PendingWorkScope.Value.HasValue)
                {
                    await serviceScope.DisposeAsync();
                    PendingWorkScope.Value = null;
                }
            }
        }

        public void RunInBackground(WeComReceiveMessage receiveMessage, Func<Task> work)
        {
            var scope = PendingWorkScope.Value;
            PendingWorkScope.Value = null;
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
                finally
                {
                    if (scope.HasValue)
                    {
                        await scope.Value.DisposeAsync();
                    }
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
