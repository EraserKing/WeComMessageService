namespace Synology.Models
{
    public class SynologySnapshot
    {
        public string? Model { get; set; }

        public string? FirmwareVersion { get; set; }

        public string? Uptime { get; set; }

        public string? CpuSeries { get; set; }

        public string? CpuCores { get; set; }

        public double? CpuFrequencyMhz { get; set; }

        public double? CpuTemperatureC { get; set; }

        public string? FanMode { get; set; }

        public List<FanReading> Fans { get; } = new();

        public List<DiskReading> Disks { get; } = new();

        public DateTimeOffset RetrievedAt { get; set; }
    }

    public record FanReading(string Name, double SpeedRpm);

    public record DiskReading(string Name, string? Model, double? TemperatureC, string? Status, string? SmartStatus);
}
