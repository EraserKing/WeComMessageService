using DebtServices.Database;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace DebtServices.Services
{

    public class CosmosDbService
    {
        public enum DbActionResult
        {
            Success,
            Duplicated,
            NotAvailable,
            Failed,
        }

        private readonly ILogger<CosmosDbService> Logger;

        private static DebtReminderContext DebtReminderContext { get; set; }

        private static object InitializeLock = new object();

        public CosmosDbService(ILogger<CosmosDbService> logger, IDbContextFactory<DebtReminderContext> debtReminderContext)
        {
            Logger = logger;

            lock (InitializeLock)

            {
                logger.LogInformation("COSMOS: Initializing...");
                DebtReminderContext ??= debtReminderContext.CreateDbContext();
            }
        }

        public async Task<DbActionResult> AddItemAsync(DebtReminderModel drm)
        {
            try
            {
                var existingItems = DebtReminderContext.DebtReminders.Where(x => x.UserName == drm.UserName && x.DebtCode == drm.DebtCode && x.ReminderType == drm.ReminderType).ToArray();
                if (existingItems.Length > 0)
                {
                    return DbActionResult.Duplicated;
                }
                else
                {
                    await DebtReminderContext.AddAsync(drm);
                    await DebtReminderContext.SaveChangesAsync();
                    return DbActionResult.Success;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to add item", ex);
                return DbActionResult.Failed;
            }
        }

        public async Task<DbActionResult> DeleteItemAsync(DebtReminderModel drm)
        {
            var existingItemsQuery = DebtReminderContext.DebtReminders.Where(x => x.UserName == drm.UserName
                && (x.DebtCode == drm.DebtCode || x.ConvertStockCode == drm.ConvertStockCode)
                && x.ReminderType == drm.ReminderType);
            try
            {
                bool isDeleted = false;
                foreach (var existingItem in existingItemsQuery)
                {
                    DebtReminderContext.Remove(existingItem);
                    isDeleted = true;
                }
                await DebtReminderContext.SaveChangesAsync();
                return isDeleted ? DbActionResult.Success : DbActionResult.NotAvailable;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to delete item", ex);
                return DbActionResult.Failed;
            }
        }

        public async Task<(DbActionResult, IEnumerable<DebtReminderModel>?)> QueryItemsAsync(string userName, ReminderType reminderType, string? debtOrStockCode = null)
        {
            var items = DebtReminderContext.DebtReminders.Where(x => x.ReminderType == reminderType);
            if (userName != null)
            {
                items = items.Where(x => x.UserName == userName);
            }
            if (debtOrStockCode != null)
            {
                items = items.Where(x => x.DebtCode == debtOrStockCode || x.ConvertStockCode == debtOrStockCode);
            }

            try
            {
                var queryResults = items.ToArray();
                return (DbActionResult.Success, queryResults);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to query item(s)", ex);
                return (DbActionResult.Failed, null);
            }
        }
    }
}
