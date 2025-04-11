using Microsoft.Extensions.Logging;
using Mikan.Exceptions;
using Qbittorrent.Services;
using System.Net;
using System.Text.RegularExpressions;

namespace Mikan.Services
{
    public class MikanService
    {
        private readonly ILogger<MikanService> Logger;
        private readonly MikanBackgroundService MikanBackgroundService;
        private readonly QbittorrentService QbittorrentService;

        public const string MikanSiteUrl = "https://mikanime.tv";

        public MikanService(ILogger<MikanService> logger, MikanBackgroundService mikanBackgroundService, QbittorrentService qbittorrentService)
        {
            Logger = logger;
            MikanBackgroundService = mikanBackgroundService;
            QbittorrentService = qbittorrentService;
        }

        public string CreateList()
        {
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", MikanBackgroundService.ListItems());
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

        public async Task<string> AddEpisodeById(string episodeId)
        {
            var taskUrl = $"{MikanSiteUrl}/Home/Episode/{episodeId}";
            HttpClient httpClient = new HttpClient();
            var episodePageResponse = await httpClient.GetAsync(taskUrl);
            var episodePageString = await episodePageResponse.Content.ReadAsStringAsync();
            var episodeMatch = new Regex(@"href=\""(.+\.torrent)\""").Match(episodePageString);
            if (episodeMatch.Success)
            {
                var torrentFinalUrl = MikanSiteUrl + episodeMatch.Groups[1].Value;
                await QbittorrentService.AddItem(torrentFinalUrl);

                var episodeNameMatch = new Regex(@"<p class=\""episode-title\"">(.+)</p>").Match(episodePageString);
                var episodeName = episodeNameMatch.Success ? WebUtility.HtmlDecode(episodeNameMatch.Groups[1].Value) : "Unknown Episode";

                Logger.LogInformation($"MIKAN: Added torrent url {torrentFinalUrl} for {episodeName}");
                return $"Added torrent for {torrentFinalUrl} for {episodeName}";
            }
            else
            {
                Logger.LogInformation($"MIKAN: No episode found for this episode ID {episodeId}");
                return $"No episode found for this episode ID {episodeId}";
            }
        }
    }
}
