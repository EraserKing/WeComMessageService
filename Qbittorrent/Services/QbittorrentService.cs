using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qbittorrent.Models;
using Qbittorrent.Models.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Utilities.Utilities;

namespace Qbittorrent.Services
{
    public class QbittorrentService
    {
        private readonly ILogger<QbittorrentService> Logger;
        private readonly IOptions<QbittorrentServiceConfiguration> Options;

        private bool FilterInWork = true;

        private readonly ValueHolder<QbittorrentServiceConfigurationSite> ActiveSiteHolder;

        private HttpClient Client { get; set; } = new HttpClient();

        public QbittorrentService(ILogger<QbittorrentService> logger, IOptions<QbittorrentServiceConfiguration> options, ValueHolder<QbittorrentServiceConfigurationSite> activeSiteHolder)
        {
            Logger = logger;
            Options = options;

            ActiveSiteHolder = activeSiteHolder;

            if (ActiveSiteHolder.Get() == null)
            {
                ActiveSiteHolder.Set(options.Value.Sites.FirstOrDefault(s => s.Default) ?? options.Value.Sites.FirstOrDefault());
            }
        }

        public void CheckActiveSite()
        {
            ArgumentNullException.ThrowIfNull(ActiveSiteHolder.Get(), "Active site");
        }

        public async Task Login()
        {
            CheckActiveSite();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.CookieContainer = new System.Net.CookieContainer();

            var client = new HttpClient(httpClientHandler);

            var loginPostContent = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("username", ActiveSiteHolder.Get()?.QbUsername),
                new KeyValuePair<string, string>("password", ActiveSiteHolder.Get()?.QbPassword)
            });
            var loginResponse = await client.PostAsync($"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/auth/login", loginPostContent);
            loginResponse.EnsureSuccessStatusCode();
            Client = client;
        }

        public string SwitchSite(string siteName)
        {
            var site = Options.Value.Sites.FirstOrDefault(s => siteName.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            if (site == null)
            {
                return $"This site {siteName} is not found";
            }

            ActiveSiteHolder.Set(site);
            return $"Active site is now {siteName}";
        }

        public string ListSites()
        {
            return string.Join(Environment.NewLine, Options.Value.Sites.Select(c => $"{(c.Name == ActiveSiteHolder.Get()?.Name ? "[✅]" : "")}{(c.Default ? "[⭐]" : "")}{c.Name}"));
        }

        public async Task<string> AddItem(Stream torrentContentStream, string fileName)
        {
            try
            {
                await Login();
                var multipartFormContent = new MultipartFormDataContent();
                var fileStreamContent = new StreamContent(torrentContentStream);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-bittorrent");
                multipartFormContent.Add(fileStreamContent, "torrents", fileName);

                var finalUrl = $"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/add";

                var addTorrentResponse = await Client.PostAsync(finalUrl, multipartFormContent);
                addTorrentResponse.EnsureSuccessStatusCode();
                Logger.LogInformation("QB: Added torrent of with file name {FileName} by {FinalUrl}", fileName, finalUrl);
                return "Done";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to add torrent of with file name {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> AddItem(string url)
        {
            try
            {
                await Login();
                var addTorrentContent = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("urls", url)
                });

                var finalUrl = $"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/add";

                var addTorrentResponse = await Client.PostAsync(finalUrl, addTorrentContent);
                addTorrentResponse.EnsureSuccessStatusCode();
                Logger.LogInformation("QB: Added torrent of with url {Url} by {FinalUrl}", url, finalUrl);
                return "Done";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to add torrent of with url {Url}", url);
                throw;
            }
        }

        public async Task<string> DeleteItem(string item, bool withFile)
        {
            try
            {
                var torrentsInfo = await ListItems();
                var itemToDelete = torrentsInfo?.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                if (itemToDelete == null)
                {
                    return "Item is not found";
                }
                else
                {
                    await Login();
                    var deleteTorrentResponse = await Client.GetAsync($"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/delete?hashes={itemToDelete.hash}&deleteFiles={withFile}");
                    deleteTorrentResponse.EnsureSuccessStatusCode();

                    return "Done";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to delete items");
                throw;
            }
        }

        public async Task<string> PauseItem(string item)
        {
            try
            {
                var torrentsInfo = await ListItems();
                var itemToPause = torrentsInfo?.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                if (itemToPause == null)
                {
                    return "Item is not found";
                }
                else
                {
                    await Login();
                    var pauseTorrentResponse = await Client.GetAsync($"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/pause?hashes={itemToPause.hash}");
                    pauseTorrentResponse.EnsureSuccessStatusCode();

                    return "Done";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to pause items");
                throw;
            }
        }

        public async Task<string> ResumeItem(string item)
        {
            try
            {
                var torrentsInfo = await ListItems();
                var itemToResume = torrentsInfo?.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                if (itemToResume == null)
                {
                    return "Item is not found";
                }
                else
                {
                    await Login();
                    var resumeTorrentResponse = await Client.GetAsync($"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/resume?hashes={itemToResume.hash}");
                    resumeTorrentResponse.EnsureSuccessStatusCode();

                    return "Done";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to resume items");
                throw;
            }
        }

        public string ToggleFilterSwitch()
        {
            FilterInWork = !FilterInWork;
            return $"Filter status now is {FilterInWork}";
        }

        public async Task<TorrentInfo[]?> ListItems()
        {
            try
            {
                await Login();
                var getTorrentsResponse = await Client.GetAsync($"{ActiveSiteHolder.Get()?.QbUrl}/api/v2/torrents/info");
                getTorrentsResponse.EnsureSuccessStatusCode();

                var torrentsInfo = await getTorrentsResponse.Content.ReadFromJsonAsync<TorrentInfo[]>();
                if (torrentsInfo != null)
                {
                    uint i = 1;
                    foreach (var torrentInfo in torrentsInfo)
                    {
                        torrentInfo.ID = i++;
                    }
                }

                if (FilterInWork && Options.Value.HiddenWords != null)
                {
                    return torrentsInfo?.ToArray().Select(ti =>
                    {
                        foreach (string hiddenWord in Options.Value.HiddenWords)
                        {
                            ti.name = ti.name.Replace(hiddenWord, "**");
                        }
                        return ti;
                    }).ToArray();
                }
                else
                {
                    return torrentsInfo;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QB: Unable to list items");
                throw;
            }
        }
    }
}
