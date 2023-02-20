﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qbittorrent.Models;
using Qbittorrent.Models.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Qbittorrent.Services
{
    public class QbittorrentService
    {
        private readonly ILogger<QbittorrentService> Logger;
        private readonly IOptions<QbittorrentServiceConfiguration> Options;

        private bool FilterInWork = true;

        private TorrentInfo[]? TorrentsInfo = null;

        private HttpClient Client { get; set; } = new HttpClient();

        public QbittorrentService(ILogger<QbittorrentService> logger, IOptions<QbittorrentServiceConfiguration> options)
        {
            Logger = logger;
            Options = options;
        }

        public async Task Login()
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
            Client = client;
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
                var addTorrentResponse = await Client.PostAsync($"{Options.Value.QbUrl}/api/v2/torrents/add", addTorrentContent);
                addTorrentResponse.EnsureSuccessStatusCode();
                Logger.LogInformation($"QB: Added torrent of with url {url}");
                return "Done";
            }
            catch (Exception ex)
            {
                Logger.LogError($"QB: Unable to add torrent of with url {url} due to {ex.Message}");
                throw;
            }
        }

        public async Task<string> DeleteItem(string item, bool withFile)
        {
            try
            {
                if (TorrentsInfo == null)
                {
                    return "List is empty";
                }
                else
                {
                    var itemToDelete = TorrentsInfo.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                    if (itemToDelete == null)
                    {
                        return "Item is not found";
                    }
                    else
                    {
                        await Login();
                        var deleteTorrentResponse = await Client.GetAsync($"{Options.Value.QbUrl}/api/v2/torrents/delete?hashes={itemToDelete.hash}&deleteFiles={withFile}");
                        deleteTorrentResponse.EnsureSuccessStatusCode();

                        return "Done";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"QB: Unable to delete items due to {ex.Message}");
                throw;
            }
        }

        public async Task<string> PauseItem(string item)
        {
            try
            {
                if (TorrentsInfo == null)
                {
                    return "List is empty";
                }
                else
                {
                    var itemToPause = TorrentsInfo.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                    if (itemToPause == null)
                    {
                        return "Item is not found";
                    }
                    else
                    {
                        await Login();
                        var pauseTorrentResponse = await Client.GetAsync($"{Options.Value.QbUrl}/api/v2/torrents/pause?hashes={itemToPause.hash}");
                        pauseTorrentResponse.EnsureSuccessStatusCode();

                        return "Done";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"QB: Unable to pause items due to {ex.Message}");
                throw;
            }
        }

        public async Task<string> ResumeItem(string item)
        {
            try
            {
                if (TorrentsInfo == null)
                {
                    return "List is empty";
                }
                else
                {
                    var itemToResume = TorrentsInfo.FirstOrDefault(ti => ti.ID == uint.Parse(item));
                    if (itemToResume == null)
                    {
                        return "Item is not found";
                    }
                    else
                    {
                        await Login();
                        var resumeTorrentResponse = await Client.GetAsync($"{Options.Value.QbUrl}/api/v2/torrents/resume?hashes={itemToResume.hash}");
                        resumeTorrentResponse.EnsureSuccessStatusCode();

                        return "Done";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"QB: Unable to resume items due to {ex.Message}");
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
                var getTorrentsResponse = await Client.GetAsync($"{Options.Value.QbUrl}/api/v2/torrents/info");
                getTorrentsResponse.EnsureSuccessStatusCode();

                var torrentsInfo = await getTorrentsResponse.Content.ReadFromJsonAsync<TorrentInfo[]>();
                if (torrentsInfo != null)
                {
                    uint i = 0;
                    foreach (var torrentInfo in torrentsInfo)
                    {
                        torrentInfo.ID = i++;
                    }
                }
                TorrentsInfo = torrentsInfo;

                if (FilterInWork && Options.Value.HiddenWords != null)
                {
                    return torrentsInfo.ToArray().Select(ti =>
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
                Logger.LogError($"QB: Unable to list items due to {ex.Message}");
                throw;
            }
        }
    }
}