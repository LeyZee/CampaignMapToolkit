using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    public class CaimeMetadata
    {
        public const string FILENAME = "caime_metadata.json";

        // Bump this whenever the schema below changes, and add a migration
        // to the Migrations table that upgrades the previous version's JObject
        // to the new schema.
        public const int CURRENT_VERSION = 3;

        [JsonProperty("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        [JsonProperty("map_data_config_path")]
        public string MapDataConfigPath { get; set; }

        [JsonProperty("campaign_map_name")]
        public string CampaignMapName { get; set; }

        // Paths to the .pack files used by the RPFM database source workflow. Always stored sorted
        // alphabetically by pack file name (see MetadataService) - not a user-chosen priority, just a
        // rare tiebreak for two packs that both define the exact same table fragment name. Independent
        // of the map_data config fields above - do not overwrite one when writing the other (see
        // MetadataService).
        [JsonProperty("pack_file_paths")]
        public List<string> PackFilePaths { get; set; } = new List<string>();

        // Converts the JObject read from disk one version forward. Keyed by
        // the version being migrated FROM. Add an entry here (and bump
        // CURRENT_VERSION) whenever the schema changes; leave older
        // migrations untouched so files written by older CAIME versions
        // keep loading correctly.
        private static readonly Dictionary<int, Action<JObject>> Migrations = new Dictionary<int, Action<JObject>>
        {
            // Version 0 (no "version" field, pre-2026-07 CAIME builds) -> 1: added the
            // explicit "version" field. No other fields changed.
            [0] = root => { },

            // Version 1 -> 2: added the optional "pack_file_path" field for the RPFM database
            // source workflow. Absent in v1 files; left null, which is the correct default.
            [1] = root => { },

            // Version 2 -> 3: replaced the single "pack_file_path" string with a "pack_file_paths"
            // list, so a project can layer more than one modded pack. Carries the old value forward
            // as the sole (highest-priority) entry rather than discarding it.
            [2] = root =>
            {
                var oldPath = root["pack_file_path"]?.Value<string>();
                root.Remove("pack_file_path");
                root["pack_file_paths"] = string.IsNullOrEmpty(oldPath)
                    ? new JArray()
                    : new JArray(oldPath);
            },
        };

        public static CaimeMetadata Load(string path)
        {
            var json = File.ReadAllText(path);
            var root = JObject.Parse(json);
            var fileVersion = root["version"]?.Value<int>() ?? 0;

            if (fileVersion > CURRENT_VERSION)
            {
                throw new NotSupportedException($"{FILENAME} is version {fileVersion}, which is newer than this version of CAIME supports (max supported version is {CURRENT_VERSION}). Please update CAIME.");
            }

            for (var v = fileVersion; v < CURRENT_VERSION; ++v)
            {
                if (!Migrations.TryGetValue(v, out var migrate))
                {
                    throw new NotSupportedException($"{FILENAME} - no migration available from version {v} to {v + 1}.");
                }

                migrate(root);
            }

            root["version"] = CURRENT_VERSION;

            return root.ToObject<CaimeMetadata>();
        }

        public void Save(string path)
        {
            Version = CURRENT_VERSION;
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
