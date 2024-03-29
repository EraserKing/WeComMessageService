﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qbittorrent.Models
{
    public class TorrentInfo
    {
        public uint ID { get; set; }

        public int added_on { get; set; }
        public long amount_left { get; set; }
        public bool auto_tmm { get; set; }
        public decimal availability { get; set; }
        public string category { get; set; }
        public long completed { get; set; }
        public int completion_on { get; set; }
        public string content_path { get; set; }
        public int dl_limit { get; set; }
        public int dlspeed { get; set; }
        public string download_path { get; set; }
        public long downloaded { get; set; }
        public long downloaded_session { get; set; }
        public int eta { get; set; }
        public bool f_l_piece_prio { get; set; }
        public bool force_start { get; set; }
        public string hash { get; set; }
        public string infohash_v1 { get; set; }
        public string infohash_v2 { get; set; }
        public int last_activity { get; set; }
        public string magnet_uri { get; set; }
        public decimal max_ratio { get; set; }
        public int max_seeding_time { get; set; }
        public string name { get; set; }
        public int num_complete { get; set; }
        public int num_incomplete { get; set; }
        public int num_leechs { get; set; }
        public int num_seeds { get; set; }
        public int priority { get; set; }
        public decimal progress { get; set; }
        public decimal ratio { get; set; }
        public decimal ratio_limit { get; set; }
        public string save_path { get; set; }
        public int seeding_time { get; set; }
        public int seeding_time_limit { get; set; }
        public int seen_complete { get; set; }
        public bool seq_dl { get; set; }
        public long size { get; set; }
        public string state { get; set; }
        public bool super_seeding { get; set; }
        public string tags { get; set; }
        public int time_active { get; set; }
        public long total_size { get; set; }
        public string tracker { get; set; }
        public int trackers_count { get; set; }
        public int up_limit { get; set; }
        public long uploaded { get; set; }
        public long uploaded_session { get; set; }
        public int upspeed { get; set; }

        public string GetDynamicSize(long size)
        {
            if (size < 1024)
            {
                return $"{size}B";
            }
            else if (size < 1024 * 1024)
            {
                return $"{size / 1024.0:0.##}KB";
            }
            else if (size < 1024 * 1024 * 1024)
            {
                return $"{size / 1024.0 / 1024.0:0.##}MB";
            }
            else
            {
                return $"{size / 1024.0 / 1024.0 / 1024.0:0.##}GB";
            }
        }

        public string GetState() => state switch
        {
            "error" => "❌",
            "uploading" => "⬆",
            "pausedUP" => "⬆⏸️",
            "stalledUP" => "⬆📮",
            "queuedUP" => "⬆🛳",
            "checkingUP" => "⬆🔍",
            "downloading" => "⬇️",
            "pausedDL" => "⬇️⏸️",
            "stalledDL" => "⬇️📮",
            "checkingDL" => "⬇️🔍",
            "queuedDL" => "⬇️🛳",
            _ => "❓"
        };
    }
}
