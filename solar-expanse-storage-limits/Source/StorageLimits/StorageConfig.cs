using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StorageLimits
{
    internal class SpoilageConfig
    {
        /// <summary>Per-resource spoilage rates in percent per day.</summary>
        public Dictionary<string, double> Rates { get; set; }
            = new Dictionary<string, double>();
    }

    internal enum EStoragePlacement
    {
        Both,
        Surface,
        Orbit
    }

    internal class StorageConfig
    {
        public double BaseCapacity { get; set; } = 3000;

        public double BaseCapacityOrbit { get; set; } = 0;

        public Dictionary<string, Dictionary<string, double>> Facilities { get; set; }
            = new Dictionary<string, Dictionary<string, double>>();

        public SpoilageConfig Spoilage { get; set; } = new SpoilageConfig();

        /// <summary>
        /// Per-resource placement restriction: "surface", "orbit", or "both".
        /// Resources not listed default to "both".
        /// </summary>
        public Dictionary<string, string> Placement { get; set; }
            = new Dictionary<string, string>();

        // Set of all resource IDs that appear in any facility grant.
        [YamlIgnore]
        internal HashSet<string> ManagedResources { get; private set; }
            = new HashSet<string>();

        // Parsed placement per resource.
        [YamlIgnore]
        internal Dictionary<string, EStoragePlacement> ResourcePlacement { get; private set; }
            = new Dictionary<string, EStoragePlacement>();

        private void BuildLookups()
        {
            ManagedResources = new HashSet<string>();
            ResourcePlacement = new Dictionary<string, EStoragePlacement>();

            foreach (var facilityKvp in Facilities)
            {
                foreach (string resourceId in facilityKvp.Value.Keys)
                    ManagedResources.Add(resourceId);
            }

            if (Placement != null)
            {
                foreach (var kvp in Placement)
                {
                    if (Enum.TryParse<EStoragePlacement>(kvp.Value, true, out var parsed))
                        ResourcePlacement[kvp.Key] = parsed;
                    else
                        Plugin.Log.LogWarning($"[StorageConfig] Unknown placement '{kvp.Value}' for {kvp.Key}, defaulting to Both.");
                }
            }
        }

        internal double GetSpoilageRate(string resourceId)
        {
            if (Spoilage?.Rates != null
                && Spoilage.Rates.TryGetValue(resourceId, out double pct))
                return pct / 100.0;
            return 0.0;
        }

        internal bool IsManaged(string resourceId)
        {
            return ManagedResources.Contains(resourceId);
        }

        internal EStoragePlacement GetPlacement(string resourceId)
        {
            if (ResourcePlacement.TryGetValue(resourceId, out var p))
                return p;
            return EStoragePlacement.Both;
        }

        public static StorageConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning($"[StorageConfig] Not found at {path} — using defaults (baseCapacity=3000, no facility grants).");
                return new StorageConfig();
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yaml = File.ReadAllText(path);
            var config = deserializer.Deserialize<StorageConfig>(yaml);
            if (config == null)
            {
                Plugin.Log.LogWarning("[StorageConfig] YAML parsed to null — using defaults.");
                return new StorageConfig();
            }

            if (config.Facilities == null)
                config.Facilities = new Dictionary<string, Dictionary<string, double>>();
            if (config.Spoilage == null)
                config.Spoilage = new SpoilageConfig();
            if (config.Placement == null)
                config.Placement = new Dictionary<string, string>();

            config.BuildLookups();

            int spoilageCount = config.Spoilage.Rates?.Count ?? 0;
            Plugin.Log.LogInfo($"[StorageConfig] Loaded: baseCapacity={config.BaseCapacity}, " +
                               $"{config.Facilities.Count} facility storage grants, " +
                               $"{spoilageCount} per-resource spoilage rates, " +
                               $"{config.ResourcePlacement.Count} placement overrides.");
            return config;
        }
    }
}
