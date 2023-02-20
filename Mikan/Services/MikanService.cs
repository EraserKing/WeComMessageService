using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mikan.Exceptions;
using Mikan.Models.Configurations;
using Qbittorrent.Services;

namespace Mikan.Services
{
    public class MikanService
    {
        private readonly ILogger<MikanService> Logger;
        private readonly IOptions<MikanServiceConfiguration> Options;
        private readonly MikanBackgroundService MikanBackgroundService;
        private readonly QbittorrentService QbittorrentService;

        public MikanService(ILogger<MikanService> logger, IOptions<MikanServiceConfiguration> options, MikanBackgroundService mikanBackgroundService, QbittorrentService qbittorrentService)
        {
            Logger = logger;
            Options = options;
            MikanBackgroundService = mikanBackgroundService;
            QbittorrentService = qbittorrentService;
        }

        public string CreateList()
        {
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", MikanBackgroundService.CacheItems.Select(ci => ci.MakeCardContent()));
        }

        public async Task Refresh()
        {
            await MikanBackgroundService.Refresh();
        }

        public async Task ClearOutDatedCache()
        {
            await MikanBackgroundService.ClearOutDatedCache();
        }

        public async Task ClearCache()
        {
            await MikanBackgroundService.ClearCache();
        }

        public async Task ForceRefresh()
        {
            await MikanBackgroundService.ClearCache();
            await MikanBackgroundService.Refresh();
        }

        public async Task<string> AddItem(string key)
        {
            if (uint.TryParse(key, out uint receivedKey))
            {
                var foundItem = MikanBackgroundService.CacheItems.FirstOrDefault(ci => ci.Key == receivedKey);
                if (foundItem != null)
                {
                    await QbittorrentService.AddItem(foundItem.Url);
                    Logger.LogInformation($"MIKAN: Added torrent {foundItem.Title}");
                    return $"Added torrent of {foundItem.Title}";

                }
                else
                {
                    Logger.LogInformation($"MIKAN: No item match key {key}");
                    throw new ExecutionException($"Unable to find the item by key {key}");
                }
            }
            else
            {
                Logger.LogError($"MIKAN: Invalid key {key}");
                throw new ExecutionException("Unable to recognize the key");
            }
        }
    }
}
