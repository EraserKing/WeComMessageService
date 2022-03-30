using DebtServices.Models;
using DebtServices.Services;

namespace DebtServices.Processors.Interfaces
{
    public interface IProcessor
    {
        Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService);

        ulong GetProcessorAgentId();
    }
}
