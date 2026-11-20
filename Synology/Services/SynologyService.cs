using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synology.Exceptions;
using Synology.Models;
using Synology.Models.Configurations;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synology.Services
{
    public class SynologyService
    {
        private const string AuthApi = "SYNO.API.Auth";
        private const string ApiInfoApi = "SYNO.API.Info";
        private const string SystemApi = "SYNO.Core.System";
        private const string FanSpeedApi = "SYNO.Core.Hardware.FanSpeed";
        private const string StorageApi = "SYNO.Storage.CGI.Storage";

        private const int MaxErrorBodyLength = 300;

        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        private static readonly Regex FanPropertyRegex = new(@"^((sys|system)_)?fan\d*(_?(speed|rpm))?(_(speed|rpm))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] FanValueKeys = ["fan_speed", "fan_rpm", "speed", "rpm", "current_speed", "value"];

        private static readonly Dictionary<string, SynoApiInfo> FallbackApis = new(StringComparer.OrdinalIgnoreCase)
        {
            [AuthApi] = new("auth.cgi", 1, 7),
            [SystemApi] = new("entry.cgi", 1, 3),
            [FanSpeedApi] = new("entry.cgi", 1, 1),
            [StorageApi] = new("entry.cgi", 1, 1)
        };

        private readonly ILogger<SynologyService> Logger;
        private readonly SynologyConfiguration Configuration;
        private readonly HttpClient Client;
        private readonly SemaphoreSlim LoginLock = new(1, 1);

        private Dictionary<string, SynoApiInfo>? ApiInfos;
        private string? SessionId;
        private string? SynoToken;
        private string? DeviceId;
        private DateTimeOffset SessionRefreshedAt;

        public SynologyService(IOptions<SynologyConfiguration> options, ILogger<SynologyService> logger)
        {
            Logger = logger;
            Configuration = options.Value;

            var handler = new HttpClientHandler();
            if (Configuration.IgnoreCertificateErrors)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            Client = new HttpClient(handler) { Timeout = RequestTimeout };
        }

        public async Task<string> GetStatusMarkdownAsync(CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            await EnsureSessionAsync(cancellationToken);

            var system = await TryCallApiAsync(SystemApi, "info", cancellationToken);
            var fans = await TryCallApiAsync(FanSpeedApi, "get", cancellationToken);
            var storage = await TryCallApiAsync(StorageApi, "load_info", cancellationToken);

            if (system is null && fans is null && storage is null)
            {
                throw new InvalidOperationException("无法从 Synology 获取任何状态信息，请检查账号权限以及 DSM 版本。");
            }

            return FormatMarkdown(CreateSnapshot(system, fans, storage));
        }

        public async Task<string> GetFanMarkdownAsync(CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            await EnsureSessionAsync(cancellationToken);

            var system = await TryCallApiAsync(SystemApi, "info", cancellationToken);
            var fans = await TryCallApiAsync(FanSpeedApi, "get", cancellationToken);

            if (system is null && fans is null)
            {
                throw new InvalidOperationException("无法从 Synology 获取风扇状态，请检查账号权限以及 DSM 版本。");
            }

            return FormatFanMarkdown(CreateSnapshot(system, fans, null));
        }

        public async Task<string> SetFanModeAsync(string mode, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            var target = NormalizeFanMode(mode);
            await EnsureSessionAsync(cancellationToken);

            var before = await ReadFanModeAsync(cancellationToken);

            try
            {
                await TryCallApiWithParametersAsync(FanSpeedApi, "set", new Dictionary<string, string>
                {
                    ["dual_fan_speed"] = target
                }, cancellationToken);
            }
            catch (SynologyApiException ex) when (ex.ErrorCode == 3712)
            {
                throw new InvalidOperationException($"DSM 不接受风扇模式 {target}，此型号可能不支持该模式。", ex);
            }

            var system = await TryCallApiAsync(SystemApi, "info", cancellationToken);
            var fans = await TryCallApiAsync(FanSpeedApi, "get", cancellationToken);

            return FormatFanChangeMarkdown(before, CreateSnapshot(system, fans, null));
        }

        private static SynologySnapshot CreateSnapshot(JsonElement? system, JsonElement? fans, JsonElement? storage)
        {
            var snapshot = new SynologySnapshot { RetrievedAt = DateTimeOffset.Now };

            if (system is JsonElement systemData)
            {
                ParseSystem(systemData, snapshot);
            }

            if (fans is JsonElement fanData)
            {
                ParseFans(fanData, snapshot);
            }

            if (storage is JsonElement storageData)
            {
                ParseDisks(storageData, snapshot);
            }

            return snapshot;
        }

        private async Task<string?> ReadFanModeAsync(CancellationToken cancellationToken)
        {
            var fans = await TryCallApiAsync(FanSpeedApi, "get", cancellationToken);
            return fans is JsonElement data ? GetString(data, "dual_fan_speed", "fan_mode") : null;
        }

        private static string NormalizeFanMode(string mode)
        {
            return mode.Trim().ToLowerInvariant() switch
            {
                "quiet" or "quietfan" or "安静" => "quietfan",
                "cool" or "coolfan" or "凉爽" => "coolfan",
                "full" or "fullfan" or "全速" => "fullfan",
                "stop" or "quietstop" or "quietstopfan" or "停转" => "quietstopfan",
                _ => throw new InvalidOperationException($"未知的风扇模式：{mode}")
            };
        }

        private string BaseUrl
        {
            get
            {
                var url = Configuration.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty;
                if (url.Length > 0 && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = $"http://{url}";
                }

                return url;
            }
        }

        private void EnsureConfigured()
        {
            if (!Configuration.IsEnabled)
            {
                throw new InvalidOperationException("Synology 服务已禁用。");
            }

            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                throw new InvalidOperationException("未配置 Synology 地址 (Synology:BaseUrl)。");
            }

            if (string.IsNullOrWhiteSpace(Configuration.Username) || string.IsNullOrWhiteSpace(Configuration.Password))
            {
                throw new InvalidOperationException("未配置 Synology 用户名或密码 (Synology:Username / Synology:Password)。");
            }
        }

        private async Task<Dictionary<string, SynoApiInfo>> GetApiInfosAsync(CancellationToken cancellationToken)
        {
            if (ApiInfos != null)
            {
                return ApiInfos;
            }

            var query = string.Join(",", FallbackApis.Keys);
            var uri = BuildUri("query.cgi", new Dictionary<string, string>
            {
                ["api"] = ApiInfoApi,
                ["version"] = "1",
                ["method"] = "query",
                ["query"] = query
            });

            try
            {
                using var document = await GetJsonDocumentAsync(uri, cancellationToken);
                var infos = new Dictionary<string, SynoApiInfo>(StringComparer.OrdinalIgnoreCase);

                if (GetData(document) is JsonElement data && data.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in data.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var path = GetString(property.Value, "path");
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        infos[property.Name] = new SynoApiInfo(
                            path,
                            GetInt(property.Value, "minVersion") ?? 1,
                            GetInt(property.Value, "maxVersion") ?? 1);
                    }
                }

                if (infos.Count == 0)
                {
                    throw new InvalidOperationException("DSM 没有返回任何 API 信息。");
                }

                Logger.LogDebug("SYNOLOGY: Discovered {Count} APIs", infos.Count);
                ApiInfos = infos;
                return infos;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex, "SYNOLOGY: Unable to query API information, falling back to default paths");
                ApiInfos = FallbackApis;
                return ApiInfos;
            }
        }

        private async Task EnsureSessionAsync(CancellationToken cancellationToken, bool forceLogin = false)
        {
            if (!forceLogin && SessionId != null && DateTimeOffset.UtcNow - SessionRefreshedAt < SessionLifetime)
            {
                return;
            }

            await LoginLock.WaitAsync(cancellationToken);
            try
            {
                if (!forceLogin && SessionId != null && DateTimeOffset.UtcNow - SessionRefreshedAt < SessionLifetime)
                {
                    return;
                }

                await LoginCoreAsync(cancellationToken);
            }
            finally
            {
                LoginLock.Release();
            }
        }

        private async Task LoginCoreAsync(CancellationToken cancellationToken)
        {
            var infos = await GetApiInfosAsync(cancellationToken);
            var info = GetApiInfo(infos, AuthApi);

            var form = new Dictionary<string, string>
            {
                ["api"] = AuthApi,
                ["version"] = Math.Max(info.MaxVersion, info.MinVersion).ToString(CultureInfo.InvariantCulture),
                ["method"] = "login",
                ["account"] = Configuration.Username.Trim(),
                ["passwd"] = Configuration.Password,
                ["session"] = "Core",
                ["format"] = "sid",
                ["enable_syno_token"] = "yes",
                ["enable_device_token"] = "yes",
                ["device_name"] = string.IsNullOrWhiteSpace(Configuration.DeviceName) ? "WeComMessageService" : Configuration.DeviceName.Trim()
            };

            if (!string.IsNullOrEmpty(DeviceId))
            {
                form["device_id"] = DeviceId;
            }

            JsonElement? data;
            try
            {
                data = await PostLoginAsync(info.Path, form, cancellationToken);
            }
            catch (SynologyApiException ex) when (ex.ErrorCode is 403 or 404 or 406)
            {
                if (ex.ErrorCode == 404 && form.ContainsKey("otp_code"))
                {
                    throw new InvalidOperationException("Synology OTP 验证码被拒绝，请检查 Synology:OtpSecret 以及 NAS 的时间设置。", ex);
                }

                var otpCode = GenerateOtpCode();
                if (string.IsNullOrEmpty(otpCode))
                {
                    throw new InvalidOperationException("Synology 登录需要 OTP 验证码，请在配置中设置 Synology:OtpSecret。", ex);
                }

                form["otp_code"] = otpCode;
                data = await PostLoginAsync(info.Path, form, cancellationToken);
            }

            if (data is not JsonElement loginData || loginData.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Synology 登录失败：响应中没有数据。");
            }

            SessionId = GetString(loginData, "sid");
            if (string.IsNullOrEmpty(SessionId))
            {
                throw new InvalidOperationException("Synology 登录失败：响应中没有 sid。");
            }

            SynoToken = GetString(loginData, "synotoken");
            DeviceId = GetString(loginData, "device_id") ?? GetString(loginData, "did") ?? DeviceId;
            SessionRefreshedAt = DateTimeOffset.UtcNow;

            Logger.LogInformation("SYNOLOGY: Logged in as {Username}", Configuration.Username);
        }

        private string? GenerateOtpCode()
        {
            if (string.IsNullOrWhiteSpace(Configuration.OtpSecret))
            {
                return null;
            }

            try
            {
                return Totp.GenerateCode(Configuration.OtpSecret, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SYNOLOGY: Unable to generate TOTP code");
                throw new InvalidOperationException($"无法根据 OtpSecret 生成 OTP 验证码：{ex.Message}", ex);
            }
        }

        private async Task<JsonElement?> PostLoginAsync(string path, Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            var uri = BuildUri(path);
            using var content = new FormUrlEncodedContent(form);
            using var response = await Client.PostAsync(uri, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Synology 登录返回 HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            using var document = JsonDocument.Parse(body);
            EnsureSuccess(document.RootElement, AuthApi, "login");
            return GetData(document);
        }

        private async Task<JsonElement?> TryCallApiAsync(string api, string method, CancellationToken cancellationToken)
        {
            await EnsureSessionAsync(cancellationToken);

            try
            {
                return await CallApiAsync(api, method, cancellationToken);
            }
            catch (SynologyApiException ex) when (ex.ErrorCode is 106 or 107)
            {
                Logger.LogInformation("SYNOLOGY: Session expired ({ErrorCode}), logging in again", ex.ErrorCode);
                await EnsureSessionAsync(cancellationToken, forceLogin: true);
                return await CallApiAsync(api, method, cancellationToken);
            }
            catch (SynologyApiException ex) when (ex.ErrorCode is 102 or 103 or 104 or 105 or 160)
            {
                // Optional data: some models/accounts do not expose every API (for example fan speed on diskless models).
                Logger.LogWarning("SYNOLOGY: API unavailable: {Message}", ex.Message);
                return null;
            }
        }

        private async Task<JsonElement?> TryCallApiWithParametersAsync(string api, string method, IReadOnlyDictionary<string, string> extraParameters, CancellationToken cancellationToken)
        {
            await EnsureSessionAsync(cancellationToken);

            try
            {
                return await CallApiAsync(api, method, extraParameters, cancellationToken);
            }
            catch (SynologyApiException ex) when (ex.ErrorCode is 106 or 107)
            {
                Logger.LogInformation("SYNOLOGY: Session expired ({ErrorCode}), logging in again", ex.ErrorCode);
                await EnsureSessionAsync(cancellationToken, forceLogin: true);
                return await CallApiAsync(api, method, extraParameters, cancellationToken);
            }
        }

        private Task<JsonElement?> CallApiAsync(string api, string method, CancellationToken cancellationToken)
        {
            return CallApiAsync(api, method, null, cancellationToken);
        }

        private async Task<JsonElement?> CallApiAsync(string api, string method, IReadOnlyDictionary<string, string>? extraParameters, CancellationToken cancellationToken)
        {
            var infos = await GetApiInfosAsync(cancellationToken);
            var info = GetApiInfo(infos, api);

            var parameters = new Dictionary<string, string>
            {
                ["api"] = api,
                ["version"] = Math.Max(info.MaxVersion, info.MinVersion).ToString(CultureInfo.InvariantCulture),
                ["method"] = method,
                ["_sid"] = SessionId ?? string.Empty
            };

            if (!string.IsNullOrEmpty(SynoToken))
            {
                parameters["SynoToken"] = SynoToken;
            }

            if (extraParameters != null)
            {
                foreach (var pair in extraParameters)
                {
                    parameters[pair.Key] = pair.Value;
                }
            }

            var uri = BuildUri(info.Path, parameters);
            using var document = await GetJsonDocumentAsync(uri, cancellationToken);
            EnsureSuccess(document.RootElement, api, method);
            return GetData(document);
        }

        private async Task<JsonDocument> GetJsonDocumentAsync(Uri uri, CancellationToken cancellationToken)
        {
            using var response = await Client.GetAsync(uri, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Synology 返回 HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            return JsonDocument.Parse(body);
        }

        private Uri BuildUri(string path, IReadOnlyDictionary<string, string>? parameters = null)
        {
            var url = $"{BaseUrl}/webapi/{path.TrimStart('/')}";
            if (parameters is { Count: > 0 })
            {
                var query = string.Join("&", parameters.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));
                url = $"{url}?{query}";
            }

            return new Uri(url);
        }

        private static SynoApiInfo GetApiInfo(IReadOnlyDictionary<string, SynoApiInfo> infos, string api)
        {
            if (infos.TryGetValue(api, out var info))
            {
                return info;
            }

            if (FallbackApis.TryGetValue(api, out var fallback))
            {
                return fallback;
            }

            throw new InvalidOperationException($"Synology API {api} 不可用。");
        }

        private static void EnsureSuccess(JsonElement root, string api, string method)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new SynologyApiException(api, method, 0);
            }

            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
            {
                int code = 0;
                if (root.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("code", out var codeElement)
                    && codeElement.TryGetInt32(out var parsedCode))
                {
                    code = parsedCode;
                }

                throw new SynologyApiException(api, method, code);
            }
        }

        private static JsonElement? GetData(JsonDocument document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("data", out var data))
            {
                return data.Clone();
            }

            // Older DSM storage endpoints return the payload at the root.
            return document.RootElement.TryGetProperty("disks", out _) ? document.RootElement.Clone() : null;
        }

        private static void ParseSystem(JsonElement data, SynologySnapshot snapshot)
        {
            snapshot.Model = GetString(data, "model");
            snapshot.FirmwareVersion = GetString(data, "firmware_ver", "firmware_version");
            snapshot.Uptime = GetString(data, "up_time", "uptime");
            snapshot.CpuSeries = GetString(data, "cpu_series", "cpu_family");
            snapshot.CpuCores = GetString(data, "cpu_cores");
            snapshot.CpuFrequencyMhz = GetNumber(data, "cpu_clock_speed", "cpu_clock_speed_mhz", "cpu_frequency");
            snapshot.CpuTemperatureC = GetNumber(data, "sys_temp", "cpu_temp", "cpu_temperature", "temperature");

            if (snapshot.CpuFrequencyMhz is <= 0)
            {
                snapshot.CpuFrequencyMhz = null;
            }

            if (snapshot.CpuTemperatureC is <= 0)
            {
                snapshot.CpuTemperatureC = null;
            }

            // DSM reports live RPM here, not on SYNO.Core.Hardware.FanSpeed (that API is mode only).
            if (TryGetProperty(data, "fan_list", out var fanList))
            {
                ExtractFanReadings(fanList, "风扇", snapshot.Fans);
            }
        }

        private static void ParseFans(JsonElement data, SynologySnapshot snapshot)
        {
            snapshot.FanMode = GetString(data, "dual_fan_speed", "fan_mode");

            if (TryGetProperty(data, "fan_speed", out var fanNode))
            {
                ExtractFanReadings(fanNode, "风扇", snapshot.Fans);
            }

            if (data.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in data.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && FanPropertyRegex.IsMatch(property.Name))
                    {
                        AddFan(snapshot.Fans, HumanizeFanName(property.Name), property.Value.GetDouble());
                    }
                }
            }
        }

        private static void ExtractFanReadings(JsonElement node, string name, List<FanReading> fans)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Number:
                    AddFan(fans, name, node.GetDouble());
                    break;

                case JsonValueKind.String:
                    if (double.TryParse(node.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        AddFan(fans, name, parsed);
                    }
                    break;

                case JsonValueKind.Array:
                    int index = 1;
                    foreach (var item in node.EnumerateArray())
                    {
                        ExtractFanReadings(item, $"{name} {index++}", fans);
                    }
                    break;

                case JsonValueKind.Object:
                    var explicitName = GetString(node, "name", "id");
                    foreach (var key in FanValueKeys)
                    {
                        if (TryGetNumber(node, key, out var value))
                        {
                            AddFan(fans, explicitName ?? name, value);
                            return;
                        }
                    }

                    foreach (var property in node.EnumerateObject())
                    {
                        if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.Array or JsonValueKind.Object
                            && FanPropertyRegex.IsMatch(property.Name))
                        {
                            ExtractFanReadings(property.Value, explicitName ?? HumanizeFanName(property.Name), fans);
                        }
                    }
                    break;
            }
        }

        private static void AddFan(List<FanReading> fans, string name, double speed)
        {
            if (speed <= 0 || fans.Any(fan => string.Equals(fan.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            fans.Add(new FanReading(name, speed));
        }

        private static void ParseDisks(JsonElement data, SynologySnapshot snapshot)
        {
            if (!TryGetProperty(data, "disks", out var disks) || disks.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int index = 1;
            foreach (var disk in disks.EnumerateArray())
            {
                var name = GetString(disk, "name", "id", "device") ?? $"硬盘 {index}";
                var model = GetString(disk, "model") ?? GetString(disk, "vendor");
                var temperature = GetNumber(disk, "temp", "temperature");
                if (temperature is <= 0)
                {
                    temperature = null;
                }

                snapshot.Disks.Add(new DiskReading(
                    name,
                    model,
                    temperature,
                    GetString(disk, "status"),
                    GetString(disk, "smart_status")));

                index++;
            }
        }

        private static string FormatMarkdown(SynologySnapshot snapshot)
        {
            var lines = new List<string>
            {
                $"## Synology{(string.IsNullOrEmpty(snapshot.Model) ? string.Empty : $" {snapshot.Model}")} 状态",
                string.Empty
            };

            if (!string.IsNullOrEmpty(snapshot.FirmwareVersion))
            {
                lines.Add($"> 固件: {snapshot.FirmwareVersion}");
            }

            if (!string.IsNullOrEmpty(snapshot.Uptime))
            {
                lines.Add($"> 运行时间: {FormatUptime(snapshot.Uptime)}");
            }

            if (lines[^1] != string.Empty)
            {
                lines.Add(string.Empty);
            }

            lines.Add("**CPU**");
            if (!string.IsNullOrEmpty(snapshot.CpuSeries) || !string.IsNullOrEmpty(snapshot.CpuCores))
            {
                var cpu = snapshot.CpuSeries ?? string.Empty;
                if (!string.IsNullOrEmpty(snapshot.CpuCores))
                {
                    cpu = $"{cpu} ({snapshot.CpuCores} 核)";
                }

                lines.Add($"- 型号: {cpu.Trim()}");
            }

            lines.Add($"- 频率: {(snapshot.CpuFrequencyMhz is double frequency ? $"{frequency:0} MHz" : "不可用")}");
            lines.Add($"- 温度: {(snapshot.CpuTemperatureC is double cpuTemperature ? $"{cpuTemperature:0.#} °C" : "不可用")}");
            lines.Add(string.Empty);

            AppendFanSection(lines, snapshot);
            lines.Add("**硬盘**");
            if (snapshot.Disks.Count == 0)
            {
                lines.Add("- 温度: 不可用");
            }
            else
            {
                foreach (var disk in snapshot.Disks)
                {
                    var details = new List<string>
                    {
                        disk.TemperatureC is double diskTemperature ? $"{diskTemperature:0.#} °C" : "温度不可用"
                    };

                    if (!string.IsNullOrEmpty(disk.Status))
                    {
                        details.Add(TranslateDiskStatus(disk.Status));
                    }

                    if (!string.IsNullOrEmpty(disk.SmartStatus) && !string.Equals(disk.SmartStatus, "normal", StringComparison.OrdinalIgnoreCase))
                    {
                        details.Add($"SMART: {disk.SmartStatus}");
                    }

                    var diskName = string.IsNullOrEmpty(disk.Model) ? disk.Name : $"{disk.Name} ({disk.Model})";
                    lines.Add($"- {diskName}: {string.Join(" · ", details)}");
                }
            }

            lines.Add(string.Empty);
            lines.Add($"> 更新时间: {snapshot.RetrievedAt:yyyy-MM-dd HH:mm:ss}");

            return string.Join("\n", lines).Trim();
        }

        private static void AppendFanSection(List<string> lines, SynologySnapshot snapshot)
        {
            lines.Add("**风扇**");
            if (snapshot.Fans.Count == 0)
            {
                lines.Add("- 转速: 不可用");
            }
            else
            {
                foreach (var fan in snapshot.Fans)
                {
                    lines.Add($"- {fan.Name}: {fan.SpeedRpm:0} RPM");
                }
            }

            if (!string.IsNullOrEmpty(snapshot.FanMode))
            {
                lines.Add($"- 模式: {TranslateFanMode(snapshot.FanMode)}");
            }

            lines.Add(string.Empty);
        }

        private static string FormatFanMarkdown(SynologySnapshot snapshot)
        {
            var lines = new List<string>
            {
                $"## Synology{(string.IsNullOrEmpty(snapshot.Model) ? string.Empty : $" {snapshot.Model}")} 风扇",
                string.Empty
            };

            AppendFanSection(lines, snapshot);
            lines.Add($"> 更新时间: {snapshot.RetrievedAt:yyyy-MM-dd HH:mm:ss}");

            return string.Join("\n", lines).Trim();
        }

        private static string FormatFanChangeMarkdown(string? previousMode, SynologySnapshot snapshot)
        {
            var lines = new List<string>
            {
                $"## Synology{(string.IsNullOrEmpty(snapshot.Model) ? string.Empty : $" {snapshot.Model}")} 风扇",
                string.Empty
            };

            var before = string.IsNullOrEmpty(previousMode) ? null : TranslateFanMode(previousMode);
            var after = string.IsNullOrEmpty(snapshot.FanMode) ? null : TranslateFanMode(snapshot.FanMode);

            if (before != null || after != null)
            {
                lines.Add(before != null && after != null && !string.Equals(before, after, StringComparison.Ordinal)
                    ? $"> 模式: {before} → {after}"
                    : $"> 模式: {after ?? before}");
                lines.Add(string.Empty);
            }

            AppendFanSection(lines, snapshot);
            lines.Add($"> 更新时间: {snapshot.RetrievedAt:yyyy-MM-dd HH:mm:ss}");

            return string.Join("\n", lines).Trim();
        }

        private static string FormatUptime(string uptime)
        {
            var parts = uptime.Split(':');
            if (parts.Length == 4 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
            {
                return $"{days} 天 {parts[1]}:{parts[2]}:{parts[3]}";
            }

            return uptime;
        }

        private static string TranslateFanMode(string mode)
        {
            return mode.ToLowerInvariant() switch
            {
                "fullfan" => "全速 (fullfan)",
                "coolfan" => "凉爽 (coolfan)",
                "quietfan" => "安静 (quietfan)",
                "quietstopfan" => "安静/停转 (quietstopfan)",
                _ => mode
            };
        }

        private static string TranslateDiskStatus(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "normal" => "正常",
                "crashed" => "已损坏",
                "degraded" => "已降级",
                "not_initialized" => "未初始化",
                "system_partition" => "系统分区",
                _ => status
            };
        }

        private static string HumanizeFanName(string propertyName)
        {
            return propertyName.ToLowerInvariant() switch
            {
                "fan_speed" or "speed" or "rpm" => "风扇",
                var name when name.EndsWith("_speed", StringComparison.Ordinal) || name.EndsWith("_rpm", StringComparison.Ordinal)
                    => name[..name.LastIndexOf('_')].Replace('_', ' ').ToUpperInvariant(),
                _ => propertyName
            };
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!TryGetProperty(element, name, out var value))
                {
                    continue;
                }

                switch (value.ValueKind)
                {
                    case JsonValueKind.String:
                        var text = value.GetString();
                        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                    case JsonValueKind.Number:
                        return value.GetRawText();
                }
            }

            return null;
        }

        private static double? GetNumber(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetNumber(element, name, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static int? GetInt(JsonElement element, string name)
        {
            return TryGetNumber(element, name, out var value) ? (int)value : null;
        }

        private static bool TryGetNumber(JsonElement element, string name, out double value)
        {
            value = 0;
            if (!TryGetProperty(element, name, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number)
            {
                return property.TryGetDouble(out value);
            }

            return property.ValueKind == JsonValueKind.String
                && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string Truncate(string text)
        {
            return text.Length <= MaxErrorBodyLength ? text : text[..MaxErrorBodyLength];
        }

        private sealed record SynoApiInfo(string Path, int MinVersion, int MaxVersion);
    }
}
