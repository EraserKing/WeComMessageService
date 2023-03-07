using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qbittorrent.Models.Configurations
{
    public class QbittorrentServiceConfiguration
    {
        public QbittorrentServiceConfigurationSite[] Sites { get; set; }
        public string[] HiddenWords { get; set; }
    }

    public class QbittorrentServiceConfigurationSite
    {
        public string Name { get; set; }
        public string QbUrl { get; set; }
        public string QbUsername { get; set; }
        public string QbPassword { get; set; }
        public bool Default { get; set; }
    }
}
