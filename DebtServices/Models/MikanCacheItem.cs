namespace DebtServices.Models
{
    public class MikanCacheItem
    {
        public DateTime ReceivedDateTime { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public uint Key { get; set; }

        public string MakeCardContent()
        {
            return $"{Key}: {Title}";
        }
    }
}
