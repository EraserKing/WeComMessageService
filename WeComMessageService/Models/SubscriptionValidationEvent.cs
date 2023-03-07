namespace WeComMessageService.Models
{
    public class SubscriptionValidationEvent
    {
        public string id { get; set; }
        public string topic { get; set; }
        public string subject { get; set; }
        public SubscriptionValidationEventData data { get; set; }
        public string eventType { get; set; }
        public DateTime eventTime { get; set; }
        public string metadataVersion { get; set; }
        public string dataVersion { get; set; }
    }

    public class SubscriptionValidationEventData
    {
        public string validationCode { get; set; }
        public string validationUrl { get; set; }
    }
}
