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
            ["״̬"] = "status",
            ["��"] = "turn_on",
            ["����"] = "turn_on",
            ["�ر�"] = "turn_off",
            ["�л�"] = "toggle",
            ["�鿴"] = "state",
            ["״̬��ѯ"] = "state",
            ["�б�"] = "list",
            ["����"] = "reload",
            ["����"] = "service",

            // Climate commands
            ["�յ�"] = "climate",
            ["�¶�"] = "temperature",
            ["ģʽ"] = "mode",
            ["Ԥ��"] = "preset",
            ["����ģʽ"] = "fan_mode",
            ["ʪ��"] = "humidity",

            // Light commands
            ["�ƹ�"] = "light",
            ["����"] = "brightness",
            ["��ɫ"] = "color",
            ["ɫ��"] = "temperature",

            // Other commands
            ["������"] = "sensor",
            ["�Զ���"] = "automation",
            ["�ű�"] = "script",
            ["����"] = "trigger",
            ["����"] = "help"
        };

        private static readonly string helpMessage = string.Join(Environment.NewLine,
            [
                "Home Assistant ���� / Commands:",
                "",
                "�������� / Basic Commands:",
                "status/״̬ - ��ȡ Home Assistant ״̬ / Get Home Assistant status",
                "turn_on/��/���� <�豸> - ���豸 / Turn on an entity",
                "turn_off/�ر� <�豸> - �ر��豸 / Turn off an entity",
                "toggle/�л� <�豸> - �л��豸״̬ / Toggle an entity",
                "state/״̬��ѯ <�豸> - ��ȡ�豸״̬ / Get entity state",
                "list/�б� <��> - �г��豸 (����: list light) / List entities",
                "reload/���� - �����豸����ӳ�� / Reload entity name mappings",
                "",
                "�յ����� / Climate Commands:",
                "climate/�յ� temperature/�¶� <�豸> <�¶�> - �����¶� / Set temperature",
                "climate/�յ� mode/ģʽ <�豸> <ģʽ> - ����HVACģʽ / Set HVAC mode",
                "climate/�յ� preset/Ԥ�� <�豸> <Ԥ��> - ����Ԥ��ģʽ / Set preset mode",
                "climate/�յ� fan_mode/����ģʽ <�豸> <ģʽ> - ���÷���ģʽ / Set fan mode",
                "",
                "�ƹ����� / Light Commands:",
                "light/�ƹ� brightness/���� <�豸> <0-255> - �������� / Set brightness",
                "light/�ƹ� color/��ɫ <�豸> <��ɫ> - ������ɫ / Set color",
                "light/�ƹ� temperature/ɫ�� <�豸> <������> - ����ɫ�� / Set color temperature",
                "",
                "�������� / Other Commands:",
                "sensor/������ <�豸> - ��ȡ������ֵ / Get sensor value",
                "automation/�Զ��� trigger/���� <�豸> - �����Զ��� / Trigger automation",
                "script/�ű� <�豸> - ���нű� / Run script",
                "service/���� <��> <����> [�豸] [����] - �����κη��� / Call any service",
                "",
                "ע��: ������ʹ���豸ID (light.living_room) ���Ѻ����� (������)",
                "Note: You can use either entity IDs or friendly names",
                "",
                "ʾ�� / Examples:",
                "�� ������ / turn_on Living Room Light",
                "�յ� �¶� �����յ� 22 / climate temperature Thermostat 22",
                "�ƹ� ���� ���ҵ� 128 / light brightness Bedroom Light 128",
                "״̬��ѯ �¶ȴ����� / state Temperature Sensor"
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
                return "Home Assistant �����ѽ��á�";
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
                            return "�÷�: turn_on <�豸ID������> / Usage: turn_on <entity_id_or_name>";
                        var entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "turn_on", entity);

                    case "turn_off":
                        if (parts.Length < 2)
                            return "�÷�: turn_off <�豸ID������> / Usage: turn_off <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "turn_off", entity);

                    case "toggle":
                        if (parts.Length < 2)
                            return "�÷�: toggle <�豸ID������> / Usage: toggle <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await CallServiceAsync("homeassistant", "toggle", entity);

                    case "state":
                        if (parts.Length < 2)
                            return "�÷�: state <�豸ID������> / Usage: state <entity_id_or_name>";
                        entity = await ResolveEntityAsync(parts[1]);
                        if (entity.StartsWith("MULTIPLE:"))
                            return entity;
                        return await GetEntityStateAsync(entity);

                    case "service":
                        if (parts.Length < 3)
                            return "�÷�: service <��> <����> [�豸ID������] [���Ӳ���] / Usage: service <domain> <service> [entity_id_or_name] [additional_params]";
                        var entityId = parts.Length > 3 ? await ResolveEntityAsync(parts[3]) : null;
                        if (entityId != null && entityId.StartsWith("MULTIPLE:"))
                            return entityId;
                        var additionalParams = parts.Length > 4 ? string.Join(" ", parts.Skip(4)) : null;
                        return await CallServiceAsync(parts[1], parts[2], entityId, additionalParams);

                    case "list":
                        if (parts.Length < 2)
                            return "�÷�: list <��> (����: list light, list sensor, list climate) / Usage: list <domain> (e.g., list light, list sensor, list climate)";
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
                return $"���� / Error: {ex.Message}";
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
                return "�÷�: climate <����> <�豸ID������> [ֵ]\n����: temperature/�¶�, mode/ģʽ, preset/Ԥ��, fan_mode/����ģʽ, humidity/ʪ��\n" +
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
                        return "�÷�: climate temperature <�豸ID������> <�¶�> / Usage: climate temperature <entity_id_or_name> <temperature>";
                    if (!double.TryParse(parts[2], out var temperature))
                        return "��Ч���¶�ֵ / Invalid temperature value";
                    return await CallServiceAsync("climate", "set_temperature", entity, $"temperature:{temperature}");

                case "mode":
                    if (parts.Length < 3)
                        return "�÷�: climate mode <�豸ID������> <ģʽ> (heat/cool/heat_cool/auto/off/dry/fan_only) / Usage: climate mode <entity_id_or_name> <mode>";
                    return await CallServiceAsync("climate", "set_hvac_mode", entity, $"hvac_mode:{parts[2]}");

                case "preset":
                    if (parts.Length < 3)
                        return "�÷�: climate preset <�豸ID������> <Ԥ��ģʽ> / Usage: climate preset <entity_id_or_name> <preset_mode>";
                    return await CallServiceAsync("climate", "set_preset_mode", entity, $"preset_mode:{parts[2]}");

                case "fan_mode":
                case "fan":
                    if (parts.Length < 3)
                        return "�÷�: climate fan_mode <�豸ID������> <����ģʽ> / Usage: climate fan_mode <entity_id_or_name> <fan_mode>";
                    return await CallServiceAsync("climate", "set_fan_mode", entity, $"fan_mode:{parts[2]}");

                case "humidity":
                    if (parts.Length < 3)
                        return "�÷�: climate humidity <�豸ID������> <ʪ��> / Usage: climate humidity <entity_id_or_name> <humidity>";
                    if (!int.TryParse(parts[2], out var humidity))
                        return "��Ч��ʪ��ֵ / Invalid humidity value";
                    return await CallServiceAsync("climate", "set_humidity", entity, $"humidity:{humidity}");

                default:
                    return "δ֪�Ŀյ�����������: temperature/�¶�, mode/ģʽ, preset/Ԥ��, fan_mode/����ģʽ, humidity/ʪ��\n" +
                           "Unknown climate action. Available: temperature, mode, preset, fan_mode, humidity";
            }
        }

        private async Task<string> HandleLightCommandAsync(string[] parts)
        {
            if (parts.Length < 2)
            {
                return "�÷�: light <����> <�豸ID������> [ֵ]\n����: brightness/����, color/��ɫ, temperature/ɫ��\n" +
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
                        return "�÷�: light brightness <�豸ID������> <0-255> / Usage: light brightness <entity_id_or_name> <0-255>";
                    if (!int.TryParse(parts[2], out var brightness) || brightness < 0 || brightness > 255)
                        return "��Ч������ֵ (0-255) / Invalid brightness value (0-255)";
                    return await CallServiceAsync("light", "turn_on", entity, $"brightness:{brightness}");

                case "color":
                    if (parts.Length < 3)
                        return "�÷�: light color <�豸ID������> <��ɫ����|#hex|rgb(r,g,b)> / Usage: light color <entity_id_or_name> <color_name|#hex|rgb(r,g,b)>";
                    return await CallServiceAsync("light", "turn_on", entity, $"color_name:{parts[2]}");

                case "temperature":
                case "temp":
                    if (parts.Length < 3)
                        return "�÷�: light temperature <�豸ID������> <ɫ�¿�����ֵ> / Usage: light temperature <entity_id_or_name> <color_temp_kelvin>";
                    if (!int.TryParse(parts[2], out var colorTemp))
                        return "��Ч��ɫ��ֵ / Invalid color temperature value";
                    return await CallServiceAsync("light", "turn_on", entity, $"color_temp_kelvin:{colorTemp}");

                default:
                    return "δ֪�ĵƹ⶯��������: brightness/����, color/��ɫ, temperature/ɫ��\n" +
                           "Unknown light action. Available: brightness, color, temperature";
            }
        }

        private async Task<string> HandleSensorCommandAsync(string[] parts)
        {
            if (parts.Length < 1)
            {
                return "�÷�: sensor <�豸ID������> / Usage: sensor <entity_id_or_name>";
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
                return "�÷�: automation <����> <�豸ID������>\n����: trigger/����, turn_on/����, turn_off/�ر�, reload/����\n" +
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
                    return "δ֪���Զ�������������: trigger/����, turn_on/����, turn_off/�ر�, reload/����\n" +
                           "Unknown automation action. Available: trigger, turn_on, turn_off, reload";
            }
        }

        private async Task<string> HandleScriptCommandAsync(string[] parts)
        {
            if (parts.Length < 1)
            {
                return "�÷�: script <�豸ID������> / Usage: script <entity_id_or_name>";
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

                var message = $"�ҵ����ƥ�������ȷָ��:\n/ Multiple matches found, please specify:\n" +
                             string.Join("\n", matchList);

                if (matches.Count > 10)
                {
                    message += $"\n... ���� {matches.Count - 10} ��ƥ���� / ... and {matches.Count - 10} more matches";
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

                var states = await HttpClient.GetFromJsonAsync<HomeAssistantState[]>($"/states");

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
                return $"�����¼��� {mappingCache.EntityCount} ���豸ӳ�� / Reloaded {mappingCache.EntityCount} entity mappings successfully";
            }
            else
            {
                return "���¼����豸ӳ��ʧ�� / Failed to reload entity mappings";
            }
        }

        private async Task<string> GetStatusAsync()
        {
            try
            {
                var status = await HttpClient.GetFromJsonAsync<HomeAssistantStatus>($"/");

                if (status != null)
                {
                    return $"Home Assistant ״̬ / Status: {status.Message}";
                }
                else
                {
                    return "�޷���ȡ Home Assistant ״̬ / Unable to get Home Assistant status";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting Home Assistant status");
                return $"��ȡ״̬ʱ���� / Error getting status: {ex.Message}";
            }
        }

        private async Task<string> ListEntitiesAsync(string domain)
        {
            try
            {
                var states = await HttpClient.GetFromJsonAsync<HomeAssistantState[]>($"/states");

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
                    return $"δ�ҵ��� '{domain}' ���豸 / No entities found for domain '{domain}'";
                }

                return $"{domain.ToUpperInvariant()} �豸 / entities:\n" + string.Join("\n", entities.Take(20)) +
                       (entities.Count > 20 ? $"\n... ���� {entities.Count - 20} �� / and {entities.Count - 20} more" : "");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing entities for domain {Domain}", domain);
                return $"�б��豸ʱ���� / Error listing entities: {ex.Message}";
            }
        }

        private async Task<string> GetEntityStateAsync(string entityId)
        {
            try
            {
                var state = await HttpClient.GetFromJsonAsync<HomeAssistantState>($"/states/{entityId}");

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
                                additionalInfo = $" (��ǰ / Current: {temp}��, Ŀ�� / Target: {targetTemp}��, ģʽ / Mode: {mode})";
                                break;

                            case "light":
                                var brightness = state.Attributes.Brightness?.ToString();
                                if (!string.IsNullOrEmpty(brightness))
                                {
                                    additionalInfo = $" (���� / Brightness: {brightness})";
                                }
                                break;
                        }
                    }

                    return $"{friendlyName}: {state.State}{unit}{additionalInfo}";
                }

                return $"�豸 {entityId} δ�ҵ� / Entity {entityId} not found";
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                return $"�豸 {entityId} δ�ҵ� / Entity {entityId} not found";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting entity state for {EntityId}", entityId);
                return $"��ȡ�豸 {entityId} ״̬ʱ���� / Error getting state for {entityId}: {ex.Message}";
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

                var response = await HttpClient.PostAsync($"/services/{domain}/{service}", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                Logger.LogInformation("Home Assistant service call response: {Response}", responseContent);

                if (string.IsNullOrEmpty(entityId))
                {
                    return $"���� {domain}.{service} ���óɹ� / Service {domain}.{service} called successfully";
                }
                else
                {
                    var friendlyName = GetFriendlyNameFromId(entityId);
                    return $"��Ϊ {friendlyName} ���÷��� {domain}.{service} / Service {domain}.{service} called for {friendlyName}";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error calling service {Domain}.{Service} for entity {EntityId}", domain, service, entityId);
                return $"���÷��� {domain}.{service} ʱ���� / Error calling service {domain}.{service}: {ex.Message}";
            }
        }

        private static string GetHelpMessage()
        {
            return helpMessage;
        }
    }
}