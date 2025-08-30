using DebtReminder.Database;
using DebtReminder.Models;
using Eastmoney.Services;
using Microsoft.Extensions.Logging;

namespace DebtReminder.Services
{
    public class DebtSubscriptionService
    {
        private readonly ILogger<DebtSubscriptionService> Logger;
        private readonly CosmosDbService<DebtReminderContext, DebtReminderModel> CosmosDbService;
        private readonly EastmoneyService EastmoneyService;

        public DebtSubscriptionService(ILogger<DebtSubscriptionService> logger, CosmosDbService<DebtReminderContext, DebtReminderModel> cosmosDbService, EastmoneyService eastmoneyService)
        {
            Logger = logger;
            CosmosDbService = cosmosDbService;
            EastmoneyService = eastmoneyService;
        }

        public async Task<string> AddSubscriptionAsync(string userName, ReminderType reminderType, string debtOrStockCode)
        {
            var item = new DebtReminderModel()
            {
                ID = Guid.NewGuid().ToString(),
                UserName = userName,
                ReminderType = reminderType
            };

            if (reminderType == ReminderType.LISTING)
            {
                var debtRecord = (await EastmoneyService.GetFullResource()).result.data.FirstOrDefault(e => e.SECURITY_CODE == debtOrStockCode || e.CONVERT_STOCK_CODE == debtOrStockCode);

                if (debtRecord == null)
                {
                    return $"无效代码 {debtOrStockCode}";
                }

                item.DebtCode = debtRecord.SECURITY_CODE;
                item.ConvertStockCode = debtRecord.CONVERT_STOCK_CODE;
                item.DebtName = debtRecord.SECURITY_NAME_ABBR;
            }
            else
            {
                item.DebtCode = debtOrStockCode;
                item.ConvertStockCode = debtOrStockCode;
            }

            var operationResult = await CosmosDbService.AddItemAsync(item, x => x.UserName == item.UserName && x.DebtCode == item.DebtCode && x.ReminderType == item.ReminderType);
            var resultString = operationResult switch
            {
                CosmosDbActionResult.Success => $"添加订阅成功：用户 {userName} 代码 {debtOrStockCode}",
                CosmosDbActionResult.Duplicated => $"重复添加订阅：用户 {userName} 代码 {debtOrStockCode} 已被添加",
                CosmosDbActionResult.Failed => $"添加订阅失败：用户 {userName} 代码 {debtOrStockCode} 无法添加",
                _ => $"未知错误：用户 {userName} 代码 {debtOrStockCode}",
            };

            Logger.LogDebug("SUBSCRIPTION: ADD " + resultString);
            return resultString;
        }

        public async Task<string> DeleteSubscriptionAsync(string userName, ReminderType reminderType, string debtOrStockCode)
        {
            var item = new DebtReminderModel()
            {
                UserName = userName,
                ReminderType = reminderType,
                DebtCode = debtOrStockCode,
                ConvertStockCode = debtOrStockCode
            };

            var operationResult = await CosmosDbService.DeleteItemAsync(item, x =>
                x.UserName == item.UserName
                && (x.DebtCode == item.DebtCode || x.ConvertStockCode == item.ConvertStockCode)
                && x.ReminderType == item.ReminderType);

            var resultString = operationResult switch
            {
                CosmosDbActionResult.Success => $"删除订阅成功：用户 {userName} 代码 {debtOrStockCode}",
                CosmosDbActionResult.NotAvailable => $"无法删除订阅：用户 {userName} 代码 {debtOrStockCode} 未曾订阅",
                CosmosDbActionResult.Failed => $"删除订阅失败：用户 {userName} 代码 {debtOrStockCode} 无法删除",
                _ => $"未知错误：用户 {userName} 代码 {debtOrStockCode}",
            };
            Logger.LogDebug("SUBSCRIPTION: DELETE " + resultString);
            return resultString;
        }

        public async Task<string> QuerySubscriptionAsync(string userName = null, ReminderType reminderType = ReminderType.LISTING, string? debtOrStockCode = null)
        {
            (var operationResult, var queryResults) = await CosmosDbService.QueryItemsAsync(x =>
                x.ReminderType == reminderType
                && (userName == null || x.UserName == userName)
                && (debtOrStockCode == null || x.DebtCode == debtOrStockCode || x.ConvertStockCode == debtOrStockCode));

            string resultString;
            switch (operationResult)
            {
                case CosmosDbActionResult.Success:
                    switch (reminderType)
                    {
                        case ReminderType.LISTING:
                            if (queryResults == null || queryResults.Count() == 0)
                            {
                                resultString = "无结果，可能未订阅";
                            }
                            else
                            {
                                resultString = string.Concat("查询结果：", Environment.NewLine,
                                    string.Join(Environment.NewLine,
                                    queryResults.Select(x =>
                                    string.Join(Environment.NewLine, $"债券代码：{x.DebtCode} 证券代码：{x.ConvertStockCode} 名称：{x.DebtName}"))));
                            }
                            break;

                        case ReminderType.RELEASE:
                            resultString = queryResults == null || queryResults.Count() == 0 ? "未订阅" : "已订阅";
                            break;

                        default:
                            Logger.LogError("SUBSCRIPTION: UNKNOWN TYPE {ReminderType} IN QUERY", reminderType);
                            resultString = "未知订阅类型";
                            break;
                    }
                    break;


                case CosmosDbActionResult.Failed:
                    resultString = $"无法执行查询：用户 {userName} 代码 {debtOrStockCode} (如果已指定)";
                    break;

                default:
                    resultString = $"未知错误：用户 {userName} 代码 {debtOrStockCode}";
                    break;
            }

            Logger.LogDebug("SUBSCRIPTION: QUERY {ResultString}", resultString);
            return resultString;
        }

        public async Task<string> QueryNewEntriesTodayAsync(ReminderType reminderType)
        {
            string messageContent;
            switch (reminderType)
            {
                case ReminderType.LISTING:
                    var newListings = await EastmoneyService.GetNewListingAsync();
                    if (newListings == null)
                    {
                        messageContent = "服务器发生内部错误";
                    }
                    else if (newListings.Length == 0)
                    {
                        messageContent = "今日无新上市";
                    }
                    else
                    {
                        messageContent = string.Join($"{Environment.NewLine}{Environment.NewLine}", newListings.Select(x => x.MakeCardContent()));
                    }
                    break;

                case ReminderType.RELEASE:
                    var newReleases = await EastmoneyService.GetNewReleasesAsync();
                    if (newReleases == null)
                    {
                        messageContent = "服务器发生内部错误";
                    }
                    else if (newReleases.Length == 0)
                    {
                        messageContent = "今日无新上市";
                    }
                    else
                    {
                        messageContent = string.Join($"{Environment.NewLine}{Environment.NewLine}", newReleases.Select(x => x.MakeCardContent()));
                    }
                    break;

                default:
                    messageContent = "未知查询类型";
                    Logger.LogError("SUBSCRIPTION: QUERY NEW ENTRIES {ReminderType}", reminderType);
                    break;
            }
            return messageContent;
        }
    }
}
