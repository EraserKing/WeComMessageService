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

        private DebtReminderContext DebtReminderContext { get; set; }

        public CosmosDbService(IDbContextFactory<DebtReminderContext> debtReminderContext)
        {
            DebtReminderContext = debtReminderContext.CreateDbContext();
        }

        public async Task<DbActionResult> AddItemAsync(DebtReminderModel drm)
        {
            var existingItems = DebtReminderContext.DebtReminders.Where(x => x.UserName == drm.UserName && x.DebtCode == drm.DebtCode && x.ReminderType == drm.ReminderType).ToArray();
            if (existingItems.Length > 0)
            {
                return DbActionResult.Duplicated;
            }
            else
            {
                try
                {
                    await DebtReminderContext.AddAsync(drm);
                    await DebtReminderContext.SaveChangesAsync();
                    return DbActionResult.Success;
                }
                catch (Exception ex)
                {
                    return DbActionResult.Failed;
                }
            }
        }

        public async Task<DbActionResult> DeleteItemAsync(DebtReminderModel drm)
        {
            var existingItems = DebtReminderContext.DebtReminders.Where(x => x.UserName == drm.UserName
            && (x.DebtCode == drm.DebtCode || x.ConvertStockCode == drm.ConvertStockCode)
            && x.ReminderType == drm.ReminderType).ToArray();
            if (existingItems.Length > 0)
            {
                try
                {
                    foreach (var existingItem in existingItems)
                    {
                        DebtReminderContext.Remove(existingItem);
                    }
                    await DebtReminderContext.SaveChangesAsync();
                    return DbActionResult.Success;
                }
                catch (Exception ex)
                {
                    return DbActionResult.Failed;
                }
            }
            else
            {
                return DbActionResult.NotAvailable;
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
                return (DbActionResult.Failed, null);
            }
        }
    }
}
