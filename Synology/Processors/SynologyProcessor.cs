using Synology.Services;
using WeComCommon.Models;
using WeComCommon.Processors.Interfaces;
using WeComCommon.Services;

namespace Synology.Processors
{
    public class SynologyProcessor : IProcessor
    {
        private static readonly string[] StatusCommands = ["stat", "status", "状态"];
        private static readonly string[] FanCommands = ["fan", "风扇"];

        private static readonly Dictionary<string, string> FanModes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["quiet"] = "quietfan",
            ["安静"] = "quietfan",
            ["quietfan"] = "quietfan",
            ["cool"] = "coolfan",
            ["凉爽"] = "coolfan",
            ["coolfan"] = "coolfan",
            ["full"] = "fullfan",
            ["全速"] = "fullfan",
            ["fullfan"] = "fullfan",
            ["stop"] = "quietstopfan",
            ["停转"] = "quietstopfan",
            ["quietstopfan"] = "quietstopfan"
        };

        private readonly SynologyService SynologyService;

        public SynologyProcessor(SynologyService synologyService)
        {
            SynologyService = synologyService;
        }

        public ulong GetProcessorAgentId() => 1000016;

        public Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            var command = receiveMessage.Content?.Trim() ?? string.Empty;

            if (StatusCommands.Any(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)))
            {
                weComService.RunInBackground(receiveMessage, async () =>
                {
                    var markdown = await SynologyService.GetStatusMarkdownAsync();
                    await SendMarkdownAsync(weComService, receiveMessage, markdown);
                });

                return Task.FromResult<WeComInstanceReply>(null!);
            }

            var parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && FanCommands.Any(c => string.Equals(c, parts[0], StringComparison.OrdinalIgnoreCase)))
            {
                if (parts.Length == 1)
                {
                    weComService.RunInBackground(receiveMessage, async () =>
                    {
                        var markdown = await SynologyService.GetFanMarkdownAsync();
                        await SendMarkdownAsync(weComService, receiveMessage, markdown);
                    });

                    return Task.FromResult<WeComInstanceReply>(null!);
                }

                if (parts.Length == 2 && FanModes.TryGetValue(parts[1], out var mode))
                {
                    weComService.RunInBackground(receiveMessage, async () =>
                    {
                        var markdown = await SynologyService.SetFanModeAsync(mode);
                        await SendMarkdownAsync(weComService, receiveMessage, markdown);
                    });

                    return Task.FromResult<WeComInstanceReply>(null!);
                }

                return Task.FromResult(WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, string.Join(Environment.NewLine, new[]
                {
                    "未知的风扇模式，有效的命令为：",
                    "fan / 风扇: 查看风扇状态",
                    "fan quiet / 风扇 安静: 安静模式",
                    "fan cool / 风扇 凉爽: 凉爽模式",
                    "fan full / 风扇 全速: 全速模式",
                    "fan stop / 风扇 停转: 安静/停转模式"
                })));
            }

            return Task.FromResult(WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, string.Join(Environment.NewLine, new[]
            {
                "未知命令，有效的命令为：",
                "stat / status / 状态: 获取 Synology NAS 的 CPU、风扇与硬盘状态",
                "fan / 风扇: 查看风扇状态",
                "fan quiet / 风扇 安静: 安静模式",
                "fan cool / 风扇 凉爽: 凉爽模式",
                "fan full / 风扇 全速: 全速模式",
                "fan stop / 风扇 停转: 安静/停转模式"
            })));
        }

        private static async Task SendMarkdownAsync(WeComService weComService, WeComReceiveMessage receiveMessage, string markdown)
        {
            foreach (var chunk in MarkdownMessageSplitter.Split(markdown))
            {
                await weComService.SendMessageAsync(WeComRegularMessage.CreateMarkdownMessage(receiveMessage.AgentID, receiveMessage.FromUserName, chunk));
            }
        }
    }
}
