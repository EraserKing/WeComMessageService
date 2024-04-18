namespace Mikan.Models
{
    public class MikanCacheItem
    {
        public DateTime ReceivedDateTime { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public int Key { get; set; }
        public string EpisodeId { get; set; }

        public string MakeCardContent(string selfSiteBaseUrl)
        {
            return (string.IsNullOrEmpty(EpisodeId) || string.IsNullOrEmpty(selfSiteBaseUrl)) ? $"{Key}: {Title}" :
                @$"<a href=""{selfSiteBaseUrl}mikan/addEpisode?episodeId={EpisodeId}"">{Key}: {Title}</a>";
        }
    }
}
