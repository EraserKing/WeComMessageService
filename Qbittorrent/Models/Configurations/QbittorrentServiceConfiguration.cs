using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qbittorrent.Models.Configurations
{
    public class QbittorrentServiceConfiguration
    {
        public string QbUrl { get; set; }
        public string QbUsername { get; set; }
        public string QbPassword { get; set; }
        public string[] HiddenWords { get; set; }
    }
}
