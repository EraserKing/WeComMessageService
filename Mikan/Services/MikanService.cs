﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mikan.Exceptions;
using Qbittorrent.Services;

namespace Mikan.Services
{
    public class MikanService
    {
        private readonly ILogger<MikanService> Logger;
        private readonly MikanBackgroundService MikanBackgroundService;
        private readonly QbittorrentService QbittorrentService;

        public MikanService(ILogger<MikanService> logger, MikanBackgroundService mikanBackgroundService, QbittorrentService qbittorrentService)
        {
            Logger = logger;
            MikanBackgroundService = mikanBackgroundService;
            QbittorrentService = qbittorrentService;
        }

        public string CreateList()
        {
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", MikanBackgroundService.AvailableItems.Select(ci => ci.MakeCardContent()));
        }

        public async Task<string> Refresh()
        {
            return await MikanBackgroundService.Refresh();
        }

        public async Task ClearOutDatedCache()
        {
            await MikanBackgroundService.ClearOutDatedCache();
        }

        public async Task ClearCache()
        {
            await MikanBackgroundService.ClearCache();
        }

        public async Task<string> ForceRefresh()
        {
            await MikanBackgroundService.ClearCache();
            return await MikanBackgroundService.Refresh();
        }

        public async Task<string> AddItem(string key)
        {
            if (uint.TryParse(key, out uint receivedKey))
            {
                var foundItem = MikanBackgroundService.AvailableItems.FirstOrDefault(ci => ci.Key == receivedKey);
                if (foundItem != null)
                {
                    HttpClient httpClient = new HttpClient();
                    var fileStream = await httpClient.GetStreamAsync(foundItem.Url);
                    await QbittorrentService.AddItem(fileStream, new Uri(foundItem.Url).LocalPath);
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
