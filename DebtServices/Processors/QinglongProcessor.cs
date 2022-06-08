using DebtServices.Exceptions.Qinglong;
using DebtServices.Models;
using DebtServices.Processors.Interfaces;
using DebtServices.Services;

namespace DebtServices.Processors
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
