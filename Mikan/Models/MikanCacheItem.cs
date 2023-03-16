namespace Mikan.Models
{
    public class MikanCacheItem
    {
        public DateTime ReceivedDateTime { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public int Key { get; set; }

        public string MakeCardContent()
        {
            return $"{Key}: {Title}";
        }
    }
}
