using HomeAssistant.Services;
using Microsoft.Extensions.Logging;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace HomeAssistant.Processors
{
    public class HomeAssistantProcessor : IProcessor
    {
        private readonly HomeAssistantService HomeAssistantService;
        private readonly ILogger<HomeAssistantProcessor> Logger;

        public HomeAssistantProcessor(HomeAssistantService homeAssistantService, ILogger<HomeAssistantProcessor> logger)
        {
            HomeAssistantService = homeAssistantService;
            Logger = logger;
        }

        public ulong GetProcessorAgentId() => 1000015;

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            new Thread(async () =>
            {
                try
                {
                    var response = await HomeAssistantService.SendCommandAsync(receiveMessage.Content);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, response));
                }
                catch (Exception ex)
                {
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                }
            }).Start();

            return null;
        }
    }
}