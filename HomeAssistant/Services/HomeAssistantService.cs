using HomeAssistant.Models;
using HomeAssistant.Models.Configurations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HomeAssistant.Services
{
    public class HomeAssistantService
    {
        private readonly HomeAssistantConfiguration Configuration;
        private readonly ILogger<HomeAssistantService> Logger;
        private readonly HttpClient HttpClient;
        private readonly IMemoryCache Cache;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
        private const string ENTITY_MAPPING_CACHE_KEY = "HomeAssistant_EntityMappings";

        // Chinese command mappings
        private readonly Dictionary<string, string> _chineseCommandMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Basic commands
            ["状态"] = "status",
            ["打开"] = "turn_on",
            ["开启"] = "turn_on",
            ["关闭"] = "turn_off",
            ["切换"] = "toggle",
            ["查看"] = "state",
            ["状态查询"] = "state",
            ["列表"] = "list",
            ["重载"] = "reload",
            ["服务"] = "service",

            // Climate commands
            ["空调"] = "climate",
            ["温度"] = "temperature",
            ["模式"] = "mode",
            ["预设"] = "preset",
            ["风扇模式"] = "fan_mode",
            ["湿度"] = "humidity",

            // Light commands
            ["灯光"] = "light",
            ["亮度"] = "brightness",
            ["颜色"] = "color",
            ["色温"] = "temperature",

            // Other commands
            ["传感器"] = "sensor",
            ["自动化"] = "automation",
            ["脚本"] = "script",
            ["触发"] = "trigger",
            ["帮助"] = "help"
        };

        private static readonly string helpMessage = string.Join(Environment.NewLine,
            [
                "Home Assistant 命令 / Commands:",
                "",
                "基本命令 / Basic Commands:",
                "status/状态 - 获取 Home Assistant 状态 / Get Home Assistant status",
                "turn_on/打开/开启 <设备> - 打开设备 / Turn on an entity",
                "turn_off/关闭 <设备> - 关闭设备 / Turn off an entity",
                "toggle/切换 <设备> - 切换设备状态 / Toggle an entity",
                "state/状态查询 <设备> - 获取设备状态 / Get entity state",
                "list/列表 <域> - 列出设备 (例如: list light) / List entities",
                "reload/重载 - 重载设备名称映射 / Reload entity name mappings",
                "",
                "空调命令 / Climate Commands:",
                "climate/空调 temperature/温度 <设备> <温度> - 设置温度 / Set temperature",
                "climate/空调 mode/模式 <设备> <模式> - 设置HVAC模式 / Set HVAC mode",
                "climate/空调 preset/预设 <设备> <预设> - 设置预设模式 / Set preset mode",
                "climate/空调 fan_mode/风扇模式 <设备> <模式> - 设置风扇模式 / Set fan mode",
                "",
                "灯光命令 / Light Commands:",
                "light/灯光 brightness/亮度 <设备> <0-255> - 设置亮度 / Set brightness",
                "light/灯光 color/颜色 <设备> <颜色> - 设置颜色 / Set color",
                "light/灯光 temperature/色温 <设备> <开尔文> - 设置色温 / Set color temperature",
                "",
                "其他命令 / Other Commands:",
                "sensor/传感器 <设备> - 获取传感器值 / Get sensor value",
                "automation/自动化 trigger/触发 <设备> - 触发自动化 / Trigger automation",
                "script/脚本 <设备> - 运行脚本 / Run script",
                "service/服务 <域> <服务> [设备] [参数] - 调用任何服务 / Call any service",
                "",
                "注意: 您可以使用设备ID (light.living_room) 或友好名称 (客厅灯)",
                "Note: You can use either entity IDs or friendly names",
                "",
                "示例 / Examples:",
                "打开 客厅灯 / turn_on Living Room Light",
                "空调 温度 客厅空调 22 / climate temperature Thermostat 22",
                "灯光 亮度 卧室灯 128 / light brightness Bedroom Light 128",
                "状态查询 温度传感器 / state Temperature Sensor"
            ]);

        public HomeAssistantService(IOptions<HomeAssistantConfiguration> configuration, ILogger<HomeAssistantService> logger, IMemoryCache cache)
        {
            Configuration = configuration.Value;
            Logger = logger;
            Cache = cache;
            HttpClient = new HttpClient();

            if (!string.IsNullOrEmpty(Configuration.BaseUrl))
            {
                logger.LogInformation("Setting Home Assistant base URL to {BaseUrl}", Configuration.BaseUrl);
                HttpClient.BaseAddress = new Uri(Configuration.BaseUrl);
            }

            if (!string.IsNullOrEmpty(Configuration.AccessToken))
            {
                logger.LogInformation("Setting Home Assistant access token for authentication");
                HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {Configuration.AccessToken}");
            }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (!Configuration.IsEnabled)
            {
                return "Home Assistant 服务已禁用。";
            }

            try
            {
                // Translate Chinese commands to English
                var parts = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                // Parse command to determine action
                if (parts.Length == 0)
                {
                    return helpMessage;
                }

                var action = TryTranslate(parts[0]).ToLowerInvariant();

                switch (action)
                {
                    case "status":
                        return await GetStatusAsync();

                    case "turn_on":
                        if (parts.Length < 2)
                            return "用法: turn_on <设备ID或名称> / Usage: turn_on <entity_id_or_name>";
                        var entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "turn_on", entity);

                    case "turn_off":
                        if (parts.Length < 2)
                            return "用法: turn_off <设备ID或名称> / Usage: turn_off <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "turn_off", entity);

                    case "toggle":
                        if (parts.Length < 2)
                            return "用法: toggle <设备ID或名称> / Usage: toggle <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "toggle", entity);

                    case "state":
                        if (parts.Length < 2)
                            return "用法: state <设备ID或名称> / Usage: state <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await GetEntityStateAsync(entity);

                    case "service":
                        if (parts.Length < 3)
                            return "用法: service <域> <服务> [设备ID或名称] [附加参数] / Usage: service <domain> <service> [entity_id_or_name] [additional_params]";
                        var entityId = parts.Length > 3 ? await ResolveEntityAsync(parts[3]) : null;
                        if (entityId != null && entityId.StartsWith("MULTIPLE:"))
                            return entityId;
                        var additionalParams = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : null;
                        return await CallServiceAsync(parts[1], parts[2], entityId, additionalParams);

                    case "list":
                        if (parts.Length < 2)
                            return "用法: list <域> (例如: list light, list sensor, list climate) / Usage: list <domain> (e.g., list light, list sensor, list climate)";
                        return await ListEntitiesAsync(parts[1]);

                    case "climate":
                        return await HandleClimateCommandAsync(parts.Skip(1).ToArray());

                    case "light":
                        return await HandleLightCommandAsync(parts.Skip(1).ToArray());

                    case "sensor":
                        return await HandleSensorCommandAsync(parts.Skip(1).ToArray());

                    case "automation":
                        return await HandleAutomationCommandAsync(parts.Skip(1).ToArray());

                    case "script":
                        return await HandleScriptCommandAsync(parts.Skip(1).ToArray());

                    case "reload":
                        return await ReloadEntityMappingAsync();

                    case "help":
                        return GetHelpMessage();

                    default:
                        return GetHelpMessage();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing Home Assistant command: {Command}", command);
                return $"错误 / Error: {ex.Message}";
            }
        }

        private string TryTranslate(string command)
        {
            if (_chineseCommandMap.TryGetValue(command.ToLowerInvariant(), out var translation))
            {
                return translation;
            }
            else
            {
                return command;
            }
        }

        private async Task<string> HandleClimateCommandAsync(string[] parts)
        {
            if (parts.Length < 2)
            {
                return "用法: climate <动作> <设备ID或名称> [值]\n动作: temperature/温度, mode/模式, preset/预设, fan_mode/风扇模式, humidity/湿度\n" +
                       "Usage: climate <action> <entity_id_or_name> [value]\nActions: temperature, mode, preset, fan_mode, humidity";
            }

            var action = TryTranslate(parts[0]);

            var entity = await ResolveEntityAsync(parts[1]);
            if (entity.StartsWith("MULTIPLE:"))
                return entity;

            switch (action)
            {
                case "temperature":
                case "temp":
                    if (parts.Length < 3)
                        return "用法: climate temperature <设备ID或名称> <温度> / Usage: climate temperature <entity_id_or_name> <temperature>";
                    if (!double.TryParse(parts[2], out var temperature))
                        return "无效的温度值 / Invalid temperature value";
                    return await CallServiceAsync("climate", "set_temperature", entity, $"temperature:{temperature}");

                case "mode":
                    if (parts.Length < 3)
                        return "用法: climate mode <设备ID或名称> <模式> (heat/cool/heat_cool/auto/off/dry/fan_only) / Usage: climate mode <entity_id_or_name> <mode>";
                    return await CallServiceAsync("climate", "set_hvac_mode", entity, $"hvac_mode:{parts[2]}");

                case "preset":
                    if (parts.Length < 3)
                        return "用法: climate preset <设备ID或名称> <预设模式> / Usage: climate preset <entity_id_or_name> <preset_mode>";
                    return await CallServiceAsync("climate", "set_preset_mode", entity, $"preset_mode:{parts[2]}");

                case "fan_mode":
                case "fan":
                    if (parts.Length < 3)
                        return "用法: climate fan_mode <设备ID或名称> <风扇模式> / Usage: climate fan_mode <entity_id_or_name> <fan_mode>";
                    return await CallServiceAsync("climate", "set_fan_mode", entity, $"fan_mode:{parts[2]}");

                case "humidity":
                    if (parts.Length < 3)
                        return "用法: climate humidity <设备ID或名称> <湿度> / Usage: climate humidity <entity_id_or_name> <humidity>";
                    if (!int.TryParse(parts[2], out var humidity))
                        return "无效的湿度值 / Invalid humidity value";
                    return await CallServiceAsync("climate", "set_humidity", entity, $"humidity:{humidity}");

                default:
                    return "未知的空调动作。可用: temperature/温度, mode/模式, preset/预设, fan_mode/风扇模式, humidity/湿度\n" +
                           "Unknown climate action. Available: temperature, mode, preset, fan_mode, humidity";
            }
        }

        private async Task<string> HandleLightCommandAsync(string[] parts)
        {
            if (parts.Length < 2)
            {
                return "用法: light <动作> <设备ID或名称> [值]\n动作: brightness/亮度, color/颜色, temperature/色温\n" +
                       "Usage: light <action> <entity_id_or_name> [value]\nActions: brightness, color, temperature";
            }

            var action = TryTranslate(parts[0]);

            var entity = await ResolveEntityAsync(parts[1]);
            if (entity.StartsWith("MULTIPLE:"))
                return entity;

            switch (action)
            {
                case "brightness":
                case "bright":
                    if (parts.Length < 3)
                        return "用法: light brightness <设备ID或名称> <0-255> / Usage: light brightness <entity_id_or_name> <0-255>";
                    if (!int.TryParse(parts[2], out var brightness) || brightness < 0 || brightness > 255)
                        return "无效的亮度值 (0-255) / Invalid brightness value (0-255)";
                    return await CallServiceAsync("light", "turn_on", entity, $"brightness:{brightness}");

                case "color":
                    if (parts.Length < 3)
                        return "用法: light color <设备ID或名称> <颜色名称|#hex|rgb(r,g,b)> / Usage: light color <entity_id_or_name> <color_name|#hex|rgb(r,g,b)>";
                    return await CallServiceAsync("light", "turn_on", entity, $"color_name:{parts[2]}");

                case "temperature":
                case "temp":
                    if (parts.Length < 3)
                        return "用法: light temperature <设备ID或名称> <色温开尔文值> / Usage: light temperature <entity_id_or_name> <color_temp_kelvin>";
                    if (!int.TryParse(parts[2], out var colorTemp))
                        return "无效的色温值 / Invalid color temperature value";
                    return await CallServiceAsync("light", "turn_on", entity, $"color_temp_kelvin:{colorTemp}");

                default:
                    return "未知的灯光动作。可用: brightness/亮度, color/颜色, temperature/色温\n" +
                           "Unknown light action. Available: brightness, color, temperature";
            }
        }

        private async Task<string> HandleSensorCommandAsync(string[] parts)
        {
            if (parts.Length < 1)
            {
                return "用法: sensor <设备ID或名称> / Usage: sensor <entity_id_or_name>";
            }

            var entity = await ResolveEntityAsync(parts[0]);
            if (entity.StartsWith("MULTIPLE:"))
                return entity;
            return await GetEntityStateAsync(entity);
        }

        private async Task<string> HandleAutomationCommandAsync(string[] parts)
        {
            if (parts.Length < 2)
            {
                return "用法: automation <动作> <设备ID或名称>\n动作: trigger/触发, turn_on/开启, turn_off/关闭, reload/重载\n" +
                       "Usage: automation <action> <entity_id_or_name>\nActions: trigger, turn_on, turn_off, reload";
            }

            var action = TryTranslate(parts[0]);

            var entity = await ResolveEntityAsync(parts[1]);
            if (entity.StartsWith("MULTIPLE:"))
                return entity;

            switch (action)
            {
                case "trigger":
                    return await CallServiceAsync("automation", "trigger", entity);

                case "turn_on":
                    return await CallServiceAsync("automation", "turn_on", entity);

                case "turn_off":
                    return await CallServiceAsync("automation", "turn_off", entity);

                case "reload":
                    return await CallServiceAsync("automation", "reload", null);

                default:
                    return "未知的自动化动作。可用: trigger/触发, turn_on/开启, turn_off/关闭, reload/重载\n" +
                           "Unknown automation action. Available: trigger, turn_on, turn_off, reload";
            }
        }

        private async Task<string> HandleScriptCommandAsync(string[] parts)
        {
            if (parts.Length < 1)
            {
                return "用法: script <设备ID或名称> / Usage: script <entity_id_or_name>";
            }

            var entity = await ResolveEntityAsync(parts[0]);
            if (entity.StartsWith("MULTIPLE:"))
                return entity;
            return await CallServiceAsync("script", "turn_on", entity);
        }

        private async Task<string> ResolveEntityAsync(string entityIdOrName)
        {
            // If it's already an entity ID format (contains dot), return as is
            if (entityIdOrName.Contains('.'))
            {
                return entityIdOrName;
            }

            // Get entity mapping from cache, load if not available
            var mappingCache = await GetEntityMappingAsync();
            if (mappingCache == null)
            {
                return entityIdOrName;
            }

            // Try exact match first (case insensitive)
            if (mappingCache.NameToIdMap.TryGetValue(entityIdOrName.ToLowerInvariant(), out var exactMatch))
            {
                return exactMatch;
            }

            // Try fuzzy matching - find all entities that contain the search term
            var searchTerm = entityIdOrName.ToLowerInvariant();
            var matches = mappingCache.NameToIdMap.Where(kvp =>
                kvp.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                // No matches found, return original (might be partial entity ID)
                return entityIdOrName;
            }
            else if (matches.Count == 1)
            {
                // Single match found
                return matches[0].Value;
            }
            else
            {
                // Multiple matches found, return formatted list for user to choose
                var matchList = matches.Take(10).Select((match, index) =>
                    $"{index + 1}. {GetFriendlyNameFromId(match.Value, mappingCache)} ({match.Value})")
                    .ToList();

                var message = $"找到多个匹配项，请明确指定:\n/ Multiple matches found, please specify:\n" +
                             string.Join("\n", matchList);

                if (matches.Count > 10)
                {
                    message += $"\n... 还有 {matches.Count - 10} 个匹配项 / ... and {matches.Count - 10} more matches";
                }

                return $"MULTIPLE:{message}";
            }
        }

        private string GetFriendlyNameFromId(string entityId, EntityMappingCache? mappingCache = null)
        {
            mappingCache ??= Cache.Get<EntityMappingCache>(ENTITY_MAPPING_CACHE_KEY);

            if (mappingCache?.IdToNameMap.TryGetValue(entityId, out var friendlyName) == true)
            {
                return friendlyName;
            }
            return entityId;
        }

        private async Task<EntityMappingCache?> GetEntityMappingAsync()
        {
            // Try to get from cache first
            var cachedMapping = Cache.Get<EntityMappingCache>(ENTITY_MAPPING_CACHE_KEY);
            if (cachedMapping != null)
            {
                return cachedMapping;
            }

            // Cache miss, load fresh data
            return await LoadEntityMappingAsync();
        }

        private async Task<EntityMappingCache?> LoadEntityMappingAsync()
        {
            try
            {
                Logger.LogInformation("Loading entity mappings from Home Assistant API...");

                var states = await HttpClient.GetFromJsonAsync<HomeAssistantState[]>($"states");

                var mappingCache = new EntityMappingCache();

                if (states != null)
                {
                    foreach (var state in states)
                    {
                        if (!string.IsNullOrEmpty(state.EntityId))
                        {
                            var friendlyName = state.Attributes?.FriendlyName ?? state.EntityId;

                            mappingCache.NameToIdMap[friendlyName.ToLowerInvariant()] = state.EntityId;
                            mappingCache.IdToNameMap[state.EntityId] = friendlyName;
                        }
                    }
                }

                // Cache the entity mappings with sliding expiration - let memory cache handle expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = _cacheExpiry,
                    Size = 1, // Relative size for cache eviction
                    Priority = CacheItemPriority.Normal
                };

                Cache.Set(ENTITY_MAPPING_CACHE_KEY, mappingCache, cacheOptions);

                Logger.LogInformation("Loaded and cached {Count} entity mappings", mappingCache.EntityCount);
                return mappingCache;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading entity mappings");
                return null;
            }
        }

        private async Task<string> ReloadEntityMappingAsync()
        {
            // Remove existing cache entry to force reload
            Cache.Remove(ENTITY_MAPPING_CACHE_KEY);

            var mappingCache = await LoadEntityMappingAsync();
            if (mappingCache != null)
            {
                return $"已重新加载 {mappingCache.EntityCount} 个设备映射 / Reloaded {mappingCache.EntityCount} entity mappings successfully";
            }
            else
            {
                return "重新加载设备映射失败 / Failed to reload entity mappings";
            }
        }

        private async Task<string> GetStatusAsync()
        {
            try
            {
                var status = await HttpClient.GetFromJsonAsync<HomeAssistantStatus>("");

                if (status != null)
                {
                    return $"Home Assistant 状态 / Status: {status.Message}";
                }
                else
                {
                    return "无法获取 Home Assistant 状态 / Unable to get Home Assistant status";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting Home Assistant status");
                return $"获取状态时出错 / Error getting status: {ex.Message}";
            }
        }

        private async Task<string> ListEntitiesAsync(string domain)
        {
            try
            {
                var states = await HttpClient.GetFromJsonAsync<HomeAssistantState[]>("states");

                var entities = new List<string>();

                if (states != null)
                {
                    foreach (var state in states)
                    {
                        if (!string.IsNullOrEmpty(state.EntityId) && state.EntityId.StartsWith($"{domain}."))
                        {
                            var friendlyName = state.Attributes?.FriendlyName ?? state.EntityId;
                            var stateValue = state.State?.ToString() ?? "";

                            entities.Add($"{friendlyName} ({state.EntityId}): {stateValue}");
                        }
                    }
                }

                if (entities.Count == 0)
                {
                    return $"未找到域 '{domain}' 的设备 / No entities found for domain '{domain}'";
                }

                return $"{domain.ToUpperInvariant()} 设备 / entities:\n" + string.Join("\n", entities.Take(20)) +
                       (entities.Count > 20 ? $"\n... 还有 {entities.Count - 20} 个 / and {entities.Count - 20} more" : "");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing entities for domain {Domain}", domain);
                return $"列表设备时出错 / Error listing entities: {ex.Message}";
            }
        }

        private async Task<string> GetEntityStateAsync(string entityId)
        {
            try
            {
                var state = await HttpClient.GetFromJsonAsync<HomeAssistantState>($"states/{entityId}");

                if (state != null && state.State != null)
                {
                    var friendlyName = state.Attributes?.FriendlyName ?? GetFriendlyNameFromId(entityId);
                    var unit = !string.IsNullOrEmpty(state.Attributes?.UnitOfMeasurement) ? state.Attributes.UnitOfMeasurement : "";
                    var additionalInfo = "";

                    if (state.Attributes != null)
                    {
                        // Add specific information based on entity type
                        var domain = entityId.Split('.')[0];
                        switch (domain)
                        {
                            case "climate":
                                var temp = state.Attributes.CurrentTemperature?.ToString();
                                var targetTemp = state.Attributes.Temperature?.ToString();
                                var mode = state.Attributes.HvacMode;
                                additionalInfo = $" (当前 / Current: {temp}°, 目标 / Target: {targetTemp}°, 模式 / Mode: {mode})";
                                break;

                            case "light":
                                var brightness = state.Attributes.Brightness?.ToString();
                                if (!string.IsNullOrEmpty(brightness))
                                {
                                    additionalInfo = $" (亮度 / Brightness: {brightness})";
                                }
                                break;
                        }
                    }

                    return $"{friendlyName}: {state.State}{unit}{additionalInfo}";
                }

                return $"设备 {entityId} 未找到 / Entity {entityId} not found";
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                return $"设备 {entityId} 未找到 / Entity {entityId} not found";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting entity state for {EntityId}", entityId);
                return $"获取设备 {entityId} 状态时出错 / Error getting state for {entityId}: {ex.Message}";
            }
        }

        private async Task<string> CallServiceAsync(string domain, string service, string? entityId = null, string? additionalParams = null)
        {
            try
            {
                var serviceData = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(entityId))
                {
                    serviceData["entity_id"] = entityId;
                }

                // Parse additional parameters
                if (!string.IsNullOrEmpty(additionalParams))
                {
                    var paramPairs = additionalParams.Split(',');
                    foreach (var pair in paramPairs)
                    {
                        var keyValue = pair.Split(':');
                        if (keyValue.Length == 2)
                        {
                            var key = keyValue[0].Trim();
                            var value = keyValue[1].Trim();

                            // Try to parse as number first, then as string
                            if (double.TryParse(value, out var numValue))
                            {
                                serviceData[key] = numValue;
                            }
                            else if (bool.TryParse(value, out var boolValue))
                            {
                                serviceData[key] = boolValue;
                            }
                            else
                            {
                                serviceData[key] = value;
                            }
                        }
                    }
                }

                var json = JsonSerializer.Serialize(serviceData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync($"services/{domain}/{service}", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                Logger.LogInformation("Home Assistant service call response: {Response}", responseContent);

                if (string.IsNullOrEmpty(entityId))
                {
                    return $"服务 {domain}.{service} 调用成功 / Service {domain}.{service} called successfully";
                }
                else
                {
                    var friendlyName = GetFriendlyNameFromId(entityId);
                    return $"已为 {friendlyName} 调用服务 {domain}.{service} / Service {domain}.{service} called for {friendlyName}";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error calling service {Domain}.{Service} for entity {EntityId}", domain, service, entityId);
                return $"调用服务 {domain}.{service} 时出错 / Error calling service {domain}.{service}: {ex.Message}";
            }
        }

        private static string GetHelpMessage()
        {
            return helpMessage;
        }
    }
}