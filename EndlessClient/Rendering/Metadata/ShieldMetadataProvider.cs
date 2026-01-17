using System;
using System.Collections.Generic;
using System.IO;
using AutomaticTypeMapper;
using EndlessClient.Rendering.Metadata.Models;
using Newtonsoft.Json;

namespace EndlessClient.Rendering.Metadata
{
    [AutoMappedType(IsSingleton = true)]
    public class ShieldMetadataProvider : IMetadataProvider<ShieldMetadata>
    {
        public IReadOnlyDictionary<int, ShieldMetadata> DefaultMetadata => _metadata;

        private readonly Dictionary<int, ShieldMetadata> _metadata;
        private readonly IGFXMetadataLoader _metadataLoader;

        public ShieldMetadataProvider(IGFXMetadataLoader metadataLoader)
        {
            _metadata = new Dictionary<int, ShieldMetadata>();
            _metadataLoader = metadataLoader;

            LoadShieldMetadataFromConfig();
        }

        private void LoadShieldMetadataFromConfig()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "shield_metadata.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<ShieldMetadataConfig>(json);
                    if (config?.BackMountedShields != null)
                    {
                        foreach (var shieldId in config.BackMountedShields)
                        {
                            _metadata[shieldId] = new ShieldMetadata(true);
                        }
                    }
                }
                catch
                {
                    // Fall back to defaults if config load fails
                    LoadDefaultMetadata();
                }
            }
            else
            {
                LoadDefaultMetadata();
            }
        }

        private void LoadDefaultMetadata()
        {
            // Default back-mounted shields (standard EO)
            _metadata[10] = new ShieldMetadata(true);  // good wings
            _metadata[11] = new ShieldMetadata(true);  // bag
            _metadata[14] = new ShieldMetadata(true);  // normal arrows
            _metadata[15] = new ShieldMetadata(true);  // frost arrows
            _metadata[16] = new ShieldMetadata(true);  // fire arrows
            _metadata[18] = new ShieldMetadata(true);  // good force wings
            _metadata[19] = new ShieldMetadata(true);  // fire force wings
        }

        public ShieldMetadata GetValueOrDefault(int graphic)
        {
            return _metadataLoader.GetMetadata<ShieldMetadata>(graphic)
                .ValueOr(DefaultMetadata.TryGetValue(graphic, out var ret) ? ret : ShieldMetadata.Default);
        }

        private class ShieldMetadataConfig
        {
            public int[] BackMountedShields { get; set; }
        }
    }
}

