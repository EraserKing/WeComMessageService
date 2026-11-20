using System.Text;
using System.Text.RegularExpressions;
using TeamsPresence.Models;
using TeamsPresence.Services;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace TeamsPresence.Processors
{
    public class TeamsPresenceProcessor : IProcessor
    {
        private static readonly string HelpMessage = string.Join(Environment.NewLine,
        [
            "Teams 状态控制命令：",
            "status / 状态: 查看当前状态",
            "available / 在线: 立即切换为 Available",
            "busy / 忙碌: 立即切换为 Busy",
            "away / 离开: 立即切换为 Away",
            "pause / 暂停: 今天停止自动切换（跨零点自动恢复）",
            "resume / 恢复: 取消暂停",
            "watch / 名单: 查看发送人和关键词",
            "watch senders add <名字>: 添加 1:1 聊天发送人",
            "watch senders remove <名字>: 移除 1:1 聊天发送人",
            "watch keywords add <@名字>: 添加关键词",
            "watch keywords remove <@名字>: 移除关键词",
            "help / 帮助: 显示本帮助"
        ]);

        private static readonly Dictionary<string, string> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["available"] = "available",
            ["free"] = "available",
            ["online"] = "available",
            ["在线"] = "available",
            ["空闲"] = "available",
            ["busy"] = "busy",
            ["忙碌"] = "busy",
            ["忙"] = "busy",
            ["away"] = "away",
            ["离开"] = "away",
            ["暂离"] = "away"
        };

        private static readonly Regex WatchRegex = new(
            @"^(?:watch|名单|监视)\s+(?<list>senders?|发送人|发送者|keywords?|关键词|关键字)(?:\s+(?<action>add|添加|增加|remove|delete|del|删除|移除)\s+(?<name>.+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly TeamsPresenceService TeamsPresenceService;

        public TeamsPresenceProcessor(TeamsPresenceService teamsPresenceService)
        {
            TeamsPresenceService = teamsPresenceService;
        }

        // WeCom app: AppId "TeamsPresence", AgentId 1000017.
        public ulong GetProcessorAgentId() => 1000017;

        public Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            var command = receiveMessage.Content?.Trim() ?? string.Empty;
            var parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var head = parts.Length > 0 ? parts[0] : string.Empty;

            if (parts.Length == 0 || Is(head, "help", "帮助", "?"))
            {
                return Task.FromResult(CreateHelpReply(receiveMessage));
            }

            if (Is(head, "watch", "名单", "监视"))
            {
                return HandleWatchAsync(command, parts, receiveMessage, weComService);
            }

            if (Is(head, "status", "状态", "查询"))
            {
                RunInBackground(weComService, receiveMessage, async () =>
                    FormatStatus(await TeamsPresenceService.GetStatusAsync()));
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            if (Is(head, "pause", "暂停"))
            {
                RunInBackground(weComService, receiveMessage, async () =>
                {
                    var paused = await TeamsPresenceService.PauseAsync();
                    return paused
                        ? "已暂停自动切换（今天有效，跨零点自动恢复）。"
                        : "自动切换未处于暂停状态。";
                });
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            if (Is(head, "resume", "恢复", "继续"))
            {
                RunInBackground(weComService, receiveMessage, async () =>
                {
                    var paused = await TeamsPresenceService.ResumeAsync();
                    return paused
                        ? "取消暂停后服务仍报告已暂停，请检查 TeamsPresence 服务。"
                        : "已取消暂停，自动切换已恢复。";
                });
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            if (StatusAliases.TryGetValue(head, out var target))
            {
                RunInBackground(weComService, receiveMessage, async () =>
                {
                    var manual = await TeamsPresenceService.SetStatusAsync(target);
                    return $"已提交 {manual}，最多 60 秒生效。";
                });
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            return Task.FromResult(CreateHelpReply(receiveMessage));
        }

        private Task<WeComInstanceReply> HandleWatchAsync(
            string command,
            string[] parts,
            WeComReceiveMessage receiveMessage,
            WeComService weComService)
        {
            if (parts.Length == 1)
            {
                RunInBackground(weComService, receiveMessage, async () =>
                    FormatWatch(await TeamsPresenceService.GetWatchAsync()));
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            var match = WatchRegex.Match(command);
            if (!match.Success)
            {
                return Task.FromResult(CreateHelpReply(receiveMessage));
            }

            var list = Is(match.Groups["list"].Value, "senders", "sender", "发送人", "发送者")
                ? TeamsPresenceWatchList.Senders
                : TeamsPresenceWatchList.Keywords;

            if (!match.Groups["action"].Success)
            {
                RunInBackground(weComService, receiveMessage, async () =>
                    FormatWatch(await TeamsPresenceService.GetWatchAsync()));
                return Task.FromResult<WeComInstanceReply>(null!);
            }

            var action = Is(match.Groups["action"].Value, "add", "添加", "增加")
                ? TeamsPresenceWatchAction.Add
                : TeamsPresenceWatchAction.Remove;
            var name = match.Groups["name"].Value.Trim();
            if (name.Length == 0)
            {
                return Task.FromResult(CreateHelpReply(receiveMessage));
            }

            RunInBackground(weComService, receiveMessage, async () =>
            {
                var watch = await TeamsPresenceService.EditWatchAsync(list, action, name);
                return FormatWatchEdit(list, action, name, watch);
            });
            return Task.FromResult<WeComInstanceReply>(null!);
        }

        private static string FormatStatus(TeamsPresenceStatus status)
        {
            var lines = new List<string>
            {
                "Teams 状态",
                $"暂停: {(status.Paused ? $"是（{status.PauseDate ?? "今天"}）" : "否")}",
                $"模式: {status.Mode ?? "（启动中）"}",
                $"状态: {status.Status ?? "（未知）"}",
                $"已应用: {status.Applied ?? "（未知）"}",
                $"窗口: {status.Window ?? "-"}",
                $"截止: {status.Cutoff ?? "-"}",
                $"更新: {status.Updated ?? "-"}"
            };

            if (!string.IsNullOrEmpty(status.Status)
                && !string.IsNullOrEmpty(status.Applied)
                && status.Status != status.Applied)
            {
                lines.Add("注意: 状态尚未生效，循环会在 60 秒内重试。");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatWatch(TeamsPresenceWatch watch)
        {
            var lines = new List<string>
            {
                $"发送人 ({watch.Senders.Count}):",
                watch.Senders.Count == 0
                    ? "  (无)"
                    : string.Join(Environment.NewLine, watch.Senders.Select(sender => "  - " + sender)),
                string.Empty,
                $"关键词 ({watch.Keywords.Count}):",
                watch.Keywords.Count == 0
                    ? "  (无)"
                    : string.Join(Environment.NewLine, watch.Keywords.Select(keyword => "  - " + keyword))
            };

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatWatchEdit(
            TeamsPresenceWatchList list,
            TeamsPresenceWatchAction action,
            string name,
            TeamsPresenceWatch watch)
        {
            var label = list == TeamsPresenceWatchList.Senders ? "发送人" : "关键词";
            var heading = watch.Changed == true
                ? $"已{(action == TeamsPresenceWatchAction.Add ? "添加" : "移除")}{label}：{name}"
                : action == TeamsPresenceWatchAction.Add
                    ? $"未修改：{label}列表已包含 {name}（大小写不敏感）。"
                    : $"未修改：{label}列表不包含 {name}。";

            return string.Join(Environment.NewLine, heading, string.Empty, FormatWatch(watch));
        }

        private static void RunInBackground(WeComService weComService, WeComReceiveMessage receiveMessage, Func<Task<string>> work)
        {
            weComService.RunInBackground(receiveMessage, async () =>
            {
                var text = await work();
                foreach (var chunk in SplitReply(text))
                {
                    await weComService.SendMessageAsync(
                        WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, chunk));
                }
            });
        }

        private static WeComInstanceReply CreateHelpReply(WeComReceiveMessage receiveMessage)
        {
            return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, HelpMessage);
        }

        /// <summary>
        /// Splits a reply into chunks below WeCom's 2048-byte text limit. Lines are
        /// kept whole; every line produced here is short (names are capped at 64
        /// characters), so no single line needs to be cut.
        /// </summary>
        private static IEnumerable<string> SplitReply(string text, int maxBytes = 1900)
        {
            if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
            {
                yield return text;
                yield break;
            }

            var chunk = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                if (chunk.Length > 0 && Encoding.UTF8.GetByteCount(chunk.ToString()) + Encoding.UTF8.GetByteCount(line) + 1 > maxBytes)
                {
                    yield return chunk.ToString();
                    chunk.Clear();
                }

                if (chunk.Length > 0)
                {
                    chunk.Append('\n');
                }

                chunk.Append(line);
            }

            if (chunk.Length > 0)
            {
                yield return chunk.ToString();
            }
        }

        private static bool Is(string value, params string[] candidates)
        {
            return candidates.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
