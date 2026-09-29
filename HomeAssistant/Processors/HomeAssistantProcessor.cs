using HomeAssistant.Services;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace HomeAssistant.Processors
{
    public class HomeAssistantProcessor : IProcessor
    {
        private readonly HomeAssistantService HomeAssistantService;

        public HomeAssistantProcessor(HomeAssistantService homeAssistantService)
        {
            HomeAssistantService = homeAssistantService;
        }

        public ulong GetProcessorAgentId() => 1000015;

        public Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            weComService.RunInBackground(receiveMessage, async () =>
            {
                var response = await HomeAssistantService.SendCommandAsync(receiveMessage.Content);
                await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, response));
            });

            return Task.FromResult<WeComInstanceReply>(null!);
        }
    }
}