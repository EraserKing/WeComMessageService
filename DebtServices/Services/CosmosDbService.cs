using DebtServices.Database;
using DebtServices.Models;
using DebtServices.Models.Configurations;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace DebtServices.Services
{
    public enum CosmosDbActionResult
    {
        Success,
        Duplicated,
        NotAvailable,
        Failed,
    }

    public class CosmosDbService<T, V> where T : DbContext where V : DbRecordBaseModel
    {
        private readonly ILogger<CosmosDbService<T, V>> Logger;

        private T DbContext;

        public CosmosDbService(ILogger<CosmosDbService<T, V>> logger, T dbContext)
        {
            logger.LogInformation($"COSMOS: Initializing context {typeof(T)} of {typeof(V)}...");

            Logger = logger;
            DbContext = dbContext;
        }

        public async Task<CosmosDbActionResult> AddItemAsync(V record, Func<V, bool> comparer)
        {
            try
            {
                var existingItems = DbContext.Set<V>().Where(comparer).ToArray();
                if (existingItems.Length > 0)
                {
                    return CosmosDbActionResult.Duplicated;
                }
                else
                {
                    await DbContext.AddAsync(record);
                    await DbContext.SaveChangesAsync();
                    return CosmosDbActionResult.Success;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to add item", ex);
                return CosmosDbActionResult.Failed;
            }
        }

        public async Task<CosmosDbActionResult> DeleteItemAsync(V record, Func<V, bool> comparer)
        {
            var existingItemsQuery = DbContext.Set<V>().Where(comparer);
            try
            {
                bool isDeleted = false;
                foreach (var existingItem in existingItemsQuery)
                {
                    DbContext.Remove(existingItem);
                    isDeleted = true;
                }
                await DbContext.SaveChangesAsync();
                return isDeleted ? CosmosDbActionResult.Success : CosmosDbActionResult.NotAvailable;
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to delete item", ex);
                return CosmosDbActionResult.Failed;
            }
        }

        public async Task<(CosmosDbActionResult, IEnumerable<V>?)> QueryItemsAsync(Func<V, bool> queryCondition)
        {
            var items = DbContext.Set<V>().Where(queryCondition);

            try
            {
                var queryResults = items.ToArray();
                return (CosmosDbActionResult.Success, queryResults);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to query item(s)", ex);
                return (CosmosDbActionResult.Failed, null);
            }
        }
    }
}
