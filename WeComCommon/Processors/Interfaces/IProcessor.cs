using WeComCommon.Models;
using WeComCommon.Services;

namespace WeComCommon.Processors.Interfaces
{
    public interface IProcessor
    {
        Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService);

        ulong GetProcessorAgentId();
    }
}
