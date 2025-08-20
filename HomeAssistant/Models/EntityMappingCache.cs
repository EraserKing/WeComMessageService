namespace HomeAssistant.Models
{
    /// <summary>
    /// Cache model for storing Home Assistant entity mappings
    /// </summary>
    public class EntityMappingCache
    {
        /// <summary>
        /// Maps friendly names (lowercase) to entity IDs
        /// </summary>
        public Dictionary<string, string> NameToIdMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maps entity IDs to friendly names
        /// </summary>
        public Dictionary<string, string> IdToNameMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Number of entities in the cache
        /// </summary>
        public int EntityCount => NameToIdMap.Count;
    }
}