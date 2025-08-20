using System.Text.Json.Serialization;

namespace HomeAssistant.Models
{
    public class HomeAssistantState
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; }
        
        [JsonPropertyName("state")]
        public string State { get; set; }
        
        [JsonPropertyName("attributes")]
        public HomeAssistantStateAttributes Attributes { get; set; }
        
        [JsonPropertyName("last_changed")]
        public DateTime LastChanged { get; set; }
        
        [JsonPropertyName("last_reported")]
        public DateTime LastReported { get; set; }
        
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; }
        
        [JsonPropertyName("context")]
        public HomeAssistantStateContext Context { get; set; }
    }

    public class HomeAssistantStateAttributes
    {
        [JsonPropertyName("friendly_name")]
        public string FriendlyName { get; set; }
        
        [JsonPropertyName("supported_features")]
        public int SupportedFeatures { get; set; }
        
        [JsonPropertyName("event_types")]
        public string[] EventTypes { get; set; }
        
        [JsonPropertyName("event_type")]
        public string EventType { get; set; }
        
        [JsonPropertyName("options")]
        public string[] Options { get; set; }
        
        [JsonPropertyName("device_class")]
        public string DeviceClass { get; set; }
        
        [JsonPropertyName("latitude")]
        public float Latitude { get; set; }
        
        [JsonPropertyName("longitude")]
        public float Longitude { get; set; }
        
        [JsonPropertyName("radius")]
        public int Radius { get; set; }
        
        [JsonPropertyName("passive")]
        public bool Passive { get; set; }
        
        [JsonPropertyName("persons")]
        public object[] Persons { get; set; }
        
        [JsonPropertyName("editable")]
        public bool Editable { get; set; }
        
        [JsonPropertyName("icon")]
        public string Icon { get; set; }
        
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("device_trackers")]
        public string[] DeviceTrackers { get; set; }
        
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
        
        [JsonPropertyName("hvac_modes")]
        public string[] HvacModes { get; set; }
        
        [JsonPropertyName("hvac_mode")]
        public string HvacMode { get; set; }
        
        [JsonPropertyName("min_temp")]
        public int MinTemp { get; set; }
        
        [JsonPropertyName("max_temp")]
        public int MaxTemp { get; set; }
        
        [JsonPropertyName("target_temp_step")]
        public int TargetTempStep { get; set; }
        
        [JsonPropertyName("fan_modes")]
        public string[] FanModes { get; set; }
        
        [JsonPropertyName("preset_modes")]
        public string[] PresetModes { get; set; }
        
        [JsonPropertyName("current_temperature")]
        public float? CurrentTemperature { get; set; }
        
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }
        
        [JsonPropertyName("fan_mode")]
        public string FanMode { get; set; }
        
        [JsonPropertyName("preset_mode")]
        public string PresetMode { get; set; }
        
        [JsonPropertyName("next_dawn")]
        public DateTime NextDawn { get; set; }
        
        [JsonPropertyName("next_dusk")]
        public DateTime NextDusk { get; set; }
        
        [JsonPropertyName("next_midnight")]
        public DateTime NextMidnight { get; set; }
        
        [JsonPropertyName("next_noon")]
        public DateTime NextNoon { get; set; }
        
        [JsonPropertyName("next_rising")]
        public DateTime NextRising { get; set; }
        
        [JsonPropertyName("next_setting")]
        public DateTime NextSetting { get; set; }
        
        [JsonPropertyName("elevation")]
        public float Elevation { get; set; }
        
        [JsonPropertyName("azimuth")]
        public float Azimuth { get; set; }
        
        [JsonPropertyName("rising")]
        public bool Rising { get; set; }
        
        [JsonPropertyName("supported_color_modes")]
        public string[] SupportedColorModes { get; set; }
        
        [JsonPropertyName("color_mode")]
        public string ColorMode { get; set; }
        
        [JsonPropertyName("min_color_temp_kelvin")]
        public int MinColorTempKelvin { get; set; }
        
        [JsonPropertyName("max_color_temp_kelvin")]
        public int MaxColorTempKelvin { get; set; }
        
        [JsonPropertyName("min_mireds")]
        public int MinMireds { get; set; }
        
        [JsonPropertyName("max_mireds")]
        public int MaxMireds { get; set; }
        
        [JsonPropertyName("effect_list")]
        public string[] EffectList { get; set; }
        
        [JsonPropertyName("effect")]
        public object Effect { get; set; }
        
        [JsonPropertyName("brightness")]
        public int? Brightness { get; set; }
        
        [JsonPropertyName("color_temp_kelvin")]
        public object ColorTempKelvin { get; set; }
        
        [JsonPropertyName("color_temp")]
        public object ColorTemp { get; set; }
        
        [JsonPropertyName("hs_color")]
        public object HsColor { get; set; }
        
        [JsonPropertyName("rgb_color")]
        public object RgbColor { get; set; }
        
        [JsonPropertyName("xy_color")]
        public object XyColor { get; set; }
        
        [JsonPropertyName("actionparams")]
        public string ActionParams { get; set; }
        
        [JsonPropertyName("min")]
        public int Min { get; set; }
        
        [JsonPropertyName("max")]
        public int Max { get; set; }
        
        [JsonPropertyName("step")]
        public int Step { get; set; }
        
        [JsonPropertyName("mode")]
        public string Mode { get; set; }
        
        [JsonPropertyName("unit_of_measurement")]
        public string UnitOfMeasurement { get; set; }
        
        [JsonPropertyName("state_class")]
        public string StateClass { get; set; }
        
        [JsonPropertyName("battery_level")]
        public int BatteryLevel { get; set; }
        
        [JsonPropertyName("battery_icon")]
        public string BatteryIcon { get; set; }
        
        [JsonPropertyName("source_type")]
        public string SourceType { get; set; }
        
        [JsonPropertyName("auto_update")]
        public bool AutoUpdate { get; set; }
        
        [JsonPropertyName("display_precision")]
        public int DisplayPrecision { get; set; }
        
        [JsonPropertyName("installed_version")]
        public string InstalledVersion { get; set; }
        
        [JsonPropertyName("in_progress")]
        public bool InProgress { get; set; }
        
        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; }
        
        [JsonPropertyName("release_summary")]
        public object ReleaseSummary { get; set; }
        
        [JsonPropertyName("release_url")]
        public string ReleaseUrl { get; set; }
        
        [JsonPropertyName("skipped_version")]
        public object SkippedVersion { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; }
        
        [JsonPropertyName("update_percentage")]
        public object UpdatePercentage { get; set; }
        
        [JsonPropertyName("entity_picture")]
        public string EntityPicture { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("all_day")]
        public bool AllDay { get; set; }
        
        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }
        
        [JsonPropertyName("end_time")]
        public string EndTime { get; set; }
        
        [JsonPropertyName("location")]
        public string Location { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("dew_point")]
        public float DewPoint { get; set; }
        
        [JsonPropertyName("temperature_unit")]
        public string TemperatureUnit { get; set; }
        
        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
        
        [JsonPropertyName("cloud_coverage")]
        public float CloudCoverage { get; set; }
        
        [JsonPropertyName("uv_index")]
        public float UvIndex { get; set; }
        
        [JsonPropertyName("pressure")]
        public float Pressure { get; set; }
        
        [JsonPropertyName("pressure_unit")]
        public string PressureUnit { get; set; }
        
        [JsonPropertyName("wind_bearing")]
        public float WindBearing { get; set; }
        
        [JsonPropertyName("wind_speed")]
        public float WindSpeed { get; set; }
        
        [JsonPropertyName("wind_speed_unit")]
        public string WindSpeedUnit { get; set; }
        
        [JsonPropertyName("visibility_unit")]
        public string VisibilityUnit { get; set; }
        
        [JsonPropertyName("precipitation_unit")]
        public string PrecipitationUnit { get; set; }
        
        [JsonPropertyName("attribution")]
        public string Attribution { get; set; }
        
        [JsonPropertyName("last_online")]
        public DateTime LastOnline { get; set; }
        
        [JsonPropertyName("level")]
        public int Level { get; set; }
        
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }
        
        [JsonPropertyName("device_name")]
        public string DeviceName { get; set; }
        
        [JsonPropertyName("auth_token")]
        public string AuthToken { get; set; }
        
        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; }
        
        [JsonPropertyName("available")]
        public bool Available { get; set; }
        
        [JsonPropertyName("next_alarm_status")]
        public string NextAlarmStatus { get; set; }
        
        [JsonPropertyName("alarm_volume")]
        public int AlarmVolume { get; set; }
        
        [JsonPropertyName("alarms")]
        public object[] Alarms { get; set; }
        
        [JsonPropertyName("next_timer_status")]
        public string NextTimerStatus { get; set; }
        
        [JsonPropertyName("timers")]
        public object[] Timers { get; set; }
        
        [JsonPropertyName("Count")]
        public int Count { get; set; }
        
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        
        [JsonPropertyName("brand")]
        public string Brand { get; set; }
        
        [JsonPropertyName("percentage")]
        public int Percentage { get; set; }
        
        [JsonPropertyName("percentage_step")]
        public int PercentageStep { get; set; }
        
        [JsonPropertyName("active")]
        public bool Active { get; set; }
        
        [JsonPropertyName("color")]
        public string Color { get; set; }
        
        [JsonPropertyName("empty")]
        public bool Empty { get; set; }
        
        [JsonPropertyName("filament_id")]
        public string FilamentId { get; set; }
        
        [JsonPropertyName("k_value")]
        public float KValue { get; set; }
        
        [JsonPropertyName("tray_weight")]
        public string TrayWeight { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("nozzle_temp_min")]
        public string NozzleTempMin { get; set; }
        
        [JsonPropertyName("nozzle_temp_max")]
        public string NozzleTempMax { get; set; }
        
        [JsonPropertyName("remain")]
        public int Remain { get; set; }
        
        [JsonPropertyName("remain_enabled")]
        public bool RemainEnabled { get; set; }
        
        [JsonPropertyName("tag_uid")]
        public string TagUid { get; set; }
        
        [JsonPropertyName("tray_uuid")]
        public string TrayUuid { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("modifier")]
        public int Modifier { get; set; }
        
        [JsonPropertyName("objects")]
        public object Objects { get; set; }
        
        [JsonPropertyName("ExternalSpool")]
        public object ExternalSpool { get; set; }
        
        [JsonPropertyName("restored")]
        public bool Restored { get; set; }
        
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }
        
        [JsonPropertyName("error")]
        public string Error { get; set; }
        
        [JsonPropertyName("malware")]
        public string Malware { get; set; }
        
        [JsonPropertyName("network")]
        public string Network { get; set; }
        
        [JsonPropertyName("securitySetting")]
        public string SecuritySetting { get; set; }
        
        [JsonPropertyName("systemCheck")]
        public string SystemCheck { get; set; }
        
        [JsonPropertyName("update")]
        public string Update { get; set; }
        
        [JsonPropertyName("userInfo")]
        public string UserInfo { get; set; }
        
        [JsonPropertyName("motion_detection")]
        public bool MotionDetection { get; set; }
        
        [JsonPropertyName("is_volume_muted")]
        public bool IsVolumeMuted { get; set; }
        
        [JsonPropertyName("entity_picture_local")]
        public object EntityPictureLocal { get; set; }
        
        [JsonPropertyName("last_reset")]
        public string LastReset { get; set; }
    }

    public class HomeAssistantStateContext
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("parent_id")]
        public object ParentId { get; set; }
        
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}
