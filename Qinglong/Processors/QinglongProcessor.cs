using Qinglong.Services;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace Qinglong.Processors
{
    public class QinglongProcessor : IProcessor
    {
        private readonly QinglongService QinglongService;

        public QinglongProcessor(QinglongService qinglongService)
        {
            QinglongService = qinglongService;
        }

        public ulong GetProcessorAgentId() => 1000006;

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            if (QinglongService.IsCommandValid(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        await QinglongService.ExecuteCommandAsync(receiveMessage.Content);
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else
            {
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, "未知命令");
            }
        }
    }
}
