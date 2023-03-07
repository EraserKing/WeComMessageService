using Qbittorrent.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace Qbittorrent.Processors
{
    public class QbittorrentProcessor : IProcessor
    {
        private Regex AddItemRegex = new Regex(@"^A (.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex DeleteItemRegex = new Regex(@"^D (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex DeleteItemWithFileRegex = new Regex(@"^DF (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex PauseItemRegex = new Regex(@"^P (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex ResumeItemRegex = new Regex(@"^R (\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex SwitchSiteRegex = new Regex(@"^S ([-\w]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly QbittorrentService QbittorrentService;

        public QbittorrentProcessor(QbittorrentService qbittorrentService)
        {
            QbittorrentService = qbittorrentService;
        }

        public ulong GetProcessorAgentId() => 1000010;

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            if (receiveMessage.Content.Equals("L", StringComparison.OrdinalIgnoreCase))
            {
                new Thread(async () =>
                {
                    var items = await QbittorrentService.ListItems();
                    if (items == null)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "No items fetched"));
                    }
                    else if (items.Length == 0)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, "Item list is empty"));
                    }
                    else
                    {
                        string message = string.Join($"{Environment.NewLine}{Environment.NewLine}", items.Select(x => $"[{x.ID}] {x.GetState()} {x.name} <{x.GetDynamicSize(x.size)}> <{x.progress * 100:0.##}%>"));
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                }).Start();
                return null;
            }
            else if (AddItemRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var url = AddItemRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = await QbittorrentService.AddItem(url);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else if (DeleteItemRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var id = DeleteItemRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = await QbittorrentService.DeleteItem(id, false);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else if (DeleteItemWithFileRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var id = DeleteItemWithFileRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = await QbittorrentService.DeleteItem(id, true);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else if (PauseItemRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var id = PauseItemRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = await QbittorrentService.PauseItem(id);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else if (ResumeItemRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var id = AddItemRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = await QbittorrentService.ResumeItem(id);
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, message));
                    }
                    catch (Exception ex)
                    {
                        await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, ex.Message));
                    }
                }).Start();
                return null;
            }
            else if (receiveMessage.Content.Equals("T", StringComparison.OrdinalIgnoreCase))
            {
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, QbittorrentService.ToggleFilterSwitch());
            }
            else if (receiveMessage.Content.Equals("S", StringComparison.OrdinalIgnoreCase))
            {
                return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, QbittorrentService.ListSites());
            }
            else if (SwitchSiteRegex.IsMatch(receiveMessage.Content))
            {
                new Thread(async () =>
                {
                    try
                    {
                        var siteName = SwitchSiteRegex.Match(receiveMessage.Content).Groups[1].Value;
                        string message = QbittorrentService.SwitchSite(siteName);
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
                "L: 显示所有项目",
                "A {URL}: 直接添加下载",
                "D {ID}: 删除项目",
                "DF {ID}: 删除项目与文件",
                "P {ID}: 暂停项目",
                "R {ID}: 恢复项目",
                "S: 列出站点",
                "S {NAME}: 切换到站点",
                "T: 切换过滤器"
            }));
        }
    }
}
