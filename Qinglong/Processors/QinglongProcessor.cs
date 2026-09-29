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

        public Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            if (receiveMessage.Content.Equals("RRT", StringComparison.OrdinalIgnoreCase))
            {
                weComService.RunInBackground(receiveMessage, async () =>
                {
                    await QinglongService.RerunTodayTasks();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Rerun today tasks request sent"));
                });
                return Task.FromResult<WeComInstanceReply>(null!);
            }
            else if (QinglongService.IsCommandValid(receiveMessage.Content))
            {
                weComService.RunInBackground(receiveMessage, async () =>
                {
                    await QinglongService.ExecuteCommandAsync(receiveMessage.Content);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Run qinglong task request sent"));
                });
                return Task.FromResult<WeComInstanceReply>(null!);
            }
            else
            {
                return Task.FromResult(WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, "未知命令"));
            }
        }
    }
}
