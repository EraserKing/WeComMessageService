using Mikan.Services;
using System.Text.RegularExpressions;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace Mikan.Processors
{
    public class MikanProcessor : IProcessor
    {
        private Regex ItemRegex = new Regex(@"^\d+$", RegexOptions.Compiled);

        private readonly MikanService MikanService;

        public MikanProcessor(MikanService mikanService)
        {
            MikanService = mikanService;
        }

        public ulong GetProcessorAgentId() => 1000009;

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            if (receiveMessage.Content.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    string message = MikanService.CreateList();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                }).Start();
                return null;
            }
            else if (receiveMessage.Content.Equals("REFRESH", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    await MikanService.Refresh();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Refreshed"));
                }).Start();
                return null;
            }
            else if (receiveMessage.Content.Equals("FORCEREFRESH", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    await MikanService.ForceRefresh();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Force Refreshed"));
                }).Start();
                return null;
            }
            else if (receiveMessage.Content.Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    await MikanService.ClearOutDatedCache();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Cleared out-dated items"));
                }).Start();
                return null;
            }
            else if (receiveMessage.Content.Equals("CLEARALL", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    await MikanService.ClearCache();
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Cleared all items"));
                }).Start();
                return null;
            }
            else if (ItemRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        string message = await MikanService.AddItem(receiveMessage.Content);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }

            return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, string.Join(Environment.NewLine, new string[]
            {
                "未知命令，有效的命令为：",
                "ALL: 显示所有当前可下载项",
                "ADD {URL}: 直接添加下载",
                "REFRESH: 立即刷新内容",
                "FORCEREFRESH: 强制重新刷新所有内容",
                "CLEAR: 清除过期缓存",
                "CLEARALL: 清除所有缓存",
                "{KEY}: 下载项目"
            }));
        }
    }
}
