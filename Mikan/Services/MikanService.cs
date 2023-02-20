using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mikan.Exceptions;
using Mikan.Models.Configurations;

namespace Mikan.Services
{
    public class MikanService
    {
        private readonly ILogger<MikanService> Logger;
        private readonly IOptions<MikanServiceConfiguration> Options;
        private readonly MikanBackgroundService MikanBackgroundService;

        public MikanService(ILogger<MikanService> logger, IOptions<MikanServiceConfiguration> options, MikanBackgroundService mikanBackgroundService)
        {
            Logger = logger;
            Options = options;
            MikanBackgroundService = mikanBackgroundService;
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

        public async Task AddItemByUrl(string url, string title = null)
        {
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.CookieContainer = new System.Net.CookieContainer();

                var client = new HttpClient(httpClientHandler);

                var loginPostContent = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("username", Options.Value.QbUsername),
                new KeyValuePair<string, string>("password", Options.Value.QbPassword)
            });
                var loginResponse = await client.PostAsync($"{Options.Value.QbUrl}/api/v2/auth/login", loginPostContent);
                loginResponse.EnsureSuccessStatusCode();

                var addTorrentContent = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("urls", url)
            });
                var addTorrentResponse = await client.PostAsync($"{Options.Value.QbUrl}/api/v2/torrents/add", addTorrentContent);
                addTorrentResponse.EnsureSuccessStatusCode();
                Logger.LogInformation($"MIKAN: Added torrent of with url {url}{(title == null ? "" : $" of title {title}")}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"MIKAN: Unable to add torrent of with url {url}{(title == null ? "" : $" of title {title}")} due to {ex.Message}");
                throw;
            }
        }

        public async Task<string> AddItem(string key)
        {
            if (uint.TryParse(key, out uint receivedKey))
            {
                var foundItem = MikanBackgroundService.CacheItems.FirstOrDefault(ci => ci.Key == receivedKey);
                if (foundItem != null)
                {
                    await AddItemByUrl(foundItem.Url, foundItem.Title);
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
