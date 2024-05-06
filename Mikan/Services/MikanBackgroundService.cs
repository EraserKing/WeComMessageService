using Microsoft.Extensions.Options;
using Microsoft.SyndicationFeed.Rss;
using Microsoft.SyndicationFeed;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Mikan.Models.Configurations;
using Mikan.Models;
using WeComCommon.Services;
using WeComCommon.Models;
using WeComCommon.Models.Configurations;
using System.Text.RegularExpressions;

namespace Mikan.Services
{
    public class MikanBackgroundService : IHostedService, IDisposable
    {

        private readonly ILogger<MikanBackgroundService> Logger;
        private readonly IOptions<MikanServiceConfiguration> Options;
        private readonly IServiceProvider ServiceProvider;

        public readonly List<MikanCacheItem> AvailableItems = new List<MikanCacheItem>();
        public readonly List<MikanCacheItem> FetchedItems = new List<MikanCacheItem>();

        private Timer RefreshTimer;
        private Timer ClearOutDatedTimer;

        private readonly Regex episodeIdRegex = new Regex(@"([a-f\d]+)\.torrent", RegexOptions.Compiled);

        public MikanBackgroundService(IServiceProvider serviceProvider, ILogger<MikanBackgroundService> logger, IOptions<MikanServiceConfiguration> options)
        {
            Logger = logger;
            Options = options;
            ServiceProvider = serviceProvider;
        }

        private SemaphoreSlim UpdateCacheItemLock = new SemaphoreSlim(1);

        public void CreateRefreshTimer()
        {
            if (RefreshTimer != null)
            {
                {
                    Logger.LogInformation("MIKAN: Current new refresh timer is not null. Dispose and re-create.");
                    RefreshTimer.Change(Timeout.Infinite, 0);
                    RefreshTimer.Dispose();
                }
            }
            RefreshTimer = new Timer(async (param) =>
            {
                Logger.LogInformation($"MIKAN: Start timer of refreshing at {DateTime.Now}");
                var content = await Refresh();
                if (content != null)
                {
                    var message = WeComRegularMessage.CreateTextMessage(Options.Value.SendByAgentId, "@all", content);
                    await ServiceProvider.GetRequiredService<WeComService>().SendMessageAsync(message);
                }
                Logger.LogInformation("MIKAN: Finish timer of refreshing routine");

            }, null, 20, 3600 * 1000);
        }

        public void CreateClearOutDatedTimer()
        {
            if (ClearOutDatedTimer != null)
            {
                {
                    Logger.LogInformation("MIKAN: Current clear out-dated timer is not null. Dispose and re-create.");
                    ClearOutDatedTimer.Change(Timeout.Infinite, 0);
                    ClearOutDatedTimer.Dispose();
                }
            }
            ClearOutDatedTimer = new Timer(async (param) =>
            {
                Logger.LogInformation($"MIKAN: Start timer of clearing out dated items at {DateTime.Now}");
                await ClearOutDatedCache();
                Logger.LogInformation("MIKAN: Finish timer of clearing out dated items routine");
            }, null, 0, 3600 * 1000);
        }

        public IEnumerable<string> ListItems()
        {
            return AvailableItems.Select(ci => ci.MakeCardContent(Options.Value.PublicHost));
        }

        public async Task<string> Refresh()
        {
            List<MikanCacheItem> newItems = new List<MikanCacheItem>();

            await UpdateCacheItemLock.WaitAsync();
            try
            {
                using var xmlReader = XmlReader.Create($"{MikanService.MikanSiteUrl}/RSS/MyBangumi?token={Options.Value.MikanToken}", new XmlReaderSettings() { Async = true });
                var feedReader = new RssFeedReader(xmlReader);
                Logger.LogInformation($"MIKAN: Fetched from Mikan RSS");

                while (await feedReader.Read())
                {
                    switch (feedReader.ElementType)
                    {
                        case SyndicationElementType.Item:
                            ISyndicationItem item = await feedReader.ReadItem();
                            var links = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure");
                            if (links != null)
                            {
                                if (!FetchedItems.Any(ci => ci.Title == item.Title))
                                {
                                    var episodeIdRegexMatch = episodeIdRegex.Match(links.Uri.AbsoluteUri);
                                    MikanCacheItem newItem = new MikanCacheItem
                                    {
                                        ReceivedDateTime = DateTime.Now,
                                        Title = item.Title,
                                        Url = links.Uri.AbsoluteUri,
                                        EpisodeId = episodeIdRegexMatch.Success ? episodeIdRegexMatch.Groups[1].Value : null,
                                        Key = AvailableItems.Count == 0 ? 1 : AvailableItems.Select(ci => ci.Key).Max() + 1
                                    };
                                    AvailableItems.Add(newItem);
                                    FetchedItems.Add(newItem);
                                    newItems.Add(newItem);
                                    Logger.LogInformation($"MIKAN: Received new item [{newItem.Key}]: {newItem.Title}, {newItem.Url}");
                                }
                                else
                                {
                                    Logger.LogTrace($"MIKAN: Received existing item {item.Title}");
                                }

                            }
                            else
                            {
                                Logger.LogWarning($"MIKAN: Received item without links: {item.Title}");
                            }
                            break;

                        default:
                            Logger.LogWarning($"MIKAN: Received unsupported feed of type ${feedReader.ElementType}");
                            break;
                    }
                }
            }
            finally
            {
                UpdateCacheItemLock.Release();
            }

            if (newItems.Count > 0)
            {
                Logger.LogInformation($"MIKAN: Found {newItems.Count} items to send {(string.IsNullOrEmpty(Options.Value.PublicHost) ? "without public host" : $"with public host {Options.Value.PublicHost}")}");
                string content = string.Join($"{Environment.NewLine}{Environment.NewLine}", newItems.Select(x => x.MakeCardContent(Options.Value.PublicHost)));
                Logger.LogTrace(content);
                return content;
            }
            else
            {
                return null;
            }
        }

        public async Task ClearOutDatedCache()
        {
            await UpdateCacheItemLock.WaitAsync();
            try
            {
                var itemsToClear = AvailableItems.Where(ci => ci.ReceivedDateTime.AddHours(Options.Value.ClearAfterHours) < DateTime.Now).ToArray();
                foreach (var itemToClear in itemsToClear)
                {
                    Logger.LogInformation($"MIKAN: Remove {itemToClear.Title} due to out-dated");
                    AvailableItems.Remove(itemToClear);
                }

                var newAvailableItems = AvailableItems.Select((x, i) => new MikanCacheItem
                {
                    Title = x.Title,
                    Key = i + 1,
                    ReceivedDateTime = x.ReceivedDateTime,
                    Url = x.Url
                }).ToArray();

                AvailableItems.Clear();
                AvailableItems.AddRange(newAvailableItems);
            }
            finally
            {
                UpdateCacheItemLock.Release();
            }
        }

        public async Task ClearCache()
        {
            await UpdateCacheItemLock.WaitAsync();
            try
            {
                AvailableItems.Clear();
            }
            finally
            {
                UpdateCacheItemLock.Release();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CreateRefreshTimer();
            CreateClearOutDatedTimer();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            RefreshTimer?.Change(Timeout.Infinite, 0);
            ClearOutDatedTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            RefreshTimer?.Dispose();
            ClearOutDatedTimer?.Dispose();
        }
    }
}
