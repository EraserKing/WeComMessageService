using DebtServices.Models;
using DebtServices.Processors.Interfaces;
using DebtServices.Services;
using System.Text.RegularExpressions;

namespace DebtServices.Processors
{
    public class DebtMessageProcessor : IProcessor
    {
        private Regex SubscribeRegex = new Regex(@"^订阅 ?(\d+)$", RegexOptions.Compiled);
        private Regex UnsubscribeRegex = new Regex(@"^取消订阅 ?(\d+)$", RegexOptions.Compiled);
        private Regex QueryRegex = new Regex(@"^查询 ?(\d+)$", RegexOptions.Compiled);

        private readonly DebtSubscriptionService DebtSubscriptionService;

        public DebtMessageProcessor(DebtSubscriptionService debtSubscriptionService)
        {
            DebtSubscriptionService = debtSubscriptionService;
        }

        public ulong GetProcessorAgentId() => 1000003;

        public async Task<WeComInstanceReply> ReplyMessageAsync(WeComReceiveMessage receiveMessage, WeComService weComService)
        {
            Match match;
            match = SubscribeRegex.Match(receiveMessage.Content);
            if (match.Success)
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.AddSubscriptionAsync(receiveMessage.FromUserName, ReminderType.LISTING, match.Groups[1].Value);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            match = UnsubscribeRegex.Match(receiveMessage.Content);
            if (match.Success)
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.DeleteSubscriptionAsync(receiveMessage.FromUserName, ReminderType.LISTING, match.Groups[1].Value);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            match = QueryRegex.Match(receiveMessage.Content);
            if (match.Success)
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.QuerySubscriptionAsync(receiveMessage.FromUserName, ReminderType.LISTING, match.Groups[1].Value);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "查询")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.QuerySubscriptionAsync(receiveMessage.FromUserName, ReminderType.LISTING);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "订阅申购")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.AddSubscriptionAsync(receiveMessage.FromUserName, ReminderType.RELEASE, "@all");
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "取消订阅申购")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.DeleteSubscriptionAsync(receiveMessage.FromUserName, ReminderType.RELEASE, "@all");
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "查询订阅申购")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.QuerySubscriptionAsync(receiveMessage.FromUserName, ReminderType.RELEASE, "@all");
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "今日申购")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.QueryNewEntriesTodayAsync(ReminderType.RELEASE);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            if (receiveMessage.Content == "今日上市")
            {
                new Thread(async () =>
                {
                    var responseMessage = await DebtSubscriptionService.QueryNewEntriesTodayAsync(ReminderType.LISTING);
                    await weComService.SendMessageAsync(WeComRegularMessage.CreateTextMessage(receiveMessage.AgentID, receiveMessage.FromUserName, responseMessage));
                }).Start();
                return null;
            }

            return WeComInstanceReply.Create(receiveMessage.ToUserName, receiveMessage.FromUserName, string.Join(Environment.NewLine, new string[]
            {
                "未知命令，有效的命令为：",
                "订阅 代码",
                "取消订阅 代码",
                "查询 代码",
                "查询",
                "订阅申购",
                "取消订阅申购",
                "查询订阅申购",
                "今日申购",
                "今日上市",
                "代码 为六位数字，债券代码或证券代码"
            }));
        }
    }
}
