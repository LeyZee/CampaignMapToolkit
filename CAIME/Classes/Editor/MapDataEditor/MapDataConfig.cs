using System;
using System.Xml;
using CAIME.Models;

namespace CAIME.Classes.Editor.MapDataEditor
{
    public class MapDataConfig
    {
        private const string MAP_DATA_CONFIG            = "map_data_config";
        private const string TOWN_DEFINITIONS           = "town_definitions";
        private const string TOWN_DEFINITION            = "town_definition";
        private const string TOWN_ATTRIBUTE_KEY         = "key";
        private const string TOWN_ATTRIBUTE_NUM_SLOTS   = "num_slots";
        private const string TOWN_ATTRIBUTE_HAS_PORT    = "has_port";

        private XmlDocument _configDoc;

        public MapDataConfig()
        {
            _configDoc = new XmlDocument();
        }

        public void WriteConfigFile(MapDataRegion[] regions, string filePath)
        {
            var rootNode = _configDoc.CreateElement(MAP_DATA_CONFIG);
            _configDoc.AppendChild(rootNode);

            var townsNode = _configDoc.CreateElement(TOWN_DEFINITIONS);
            rootNode.AppendChild(townsNode);

            foreach (var region in regions)
            {
                var townNode = CreateTownNode(region.Name, region.SlotsCount, region.HasPort);
                townsNode.AppendChild(townNode);
            }

            _configDoc.Save(filePath);
        }

        public bool ApplyConfigFile(string filePath, out MapDataRegion[] regions)
        {
            regions = null;

            if (string.IsNullOrEmpty(filePath))
            {
                LoggerViewModel.Log("Apply Map Data Config File error - no file was provided.", LogLevel.Error);
                return false;
            }

            try
            {
                _configDoc.Load(filePath);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log(ex.Message, LogLevel.ErrorMessageBox);
                return false;
            }

            if (_configDoc.HasChildNodes == false)
            {
                var message = $"MapDataConfig.ApplyConfigFile() - Failed to read {filePath}.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return false;
            }

            var rootNode = _configDoc[MAP_DATA_CONFIG];
            if (rootNode == null || rootNode.HasChildNodes == false)
            {
                var message = $"MapDataConfig.ApplyConfigFile() - Failed to read {filePath}.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return false;
            }

            var townsNode = rootNode[TOWN_DEFINITIONS];
            if (townsNode == null || townsNode.HasChildNodes == false)
            {
                var message = $"MapDataConfig.ApplyConfigFile() - Failed to read {filePath}.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return false;
            }

            regions = new MapDataRegion[townsNode.ChildNodes.Count];
            for (int i = 0; i < regions.Length; ++i)
            {
                var townNode = townsNode.ChildNodes[i];
                var townNameAttr = townNode.Attributes[TOWN_ATTRIBUTE_KEY];
                var numSlotsAttr = townNode.Attributes[TOWN_ATTRIBUTE_NUM_SLOTS];
                var hasPortAttr = townNode.Attributes[TOWN_ATTRIBUTE_HAS_PORT];

                if (townNameAttr == null || numSlotsAttr == null)
                {
                    var message = $"MapDataConfig.ApplyConfigFile() - {TOWN_ATTRIBUTE_KEY} or {TOWN_ATTRIBUTE_NUM_SLOTS} attributes are missing. Skipping...";
                    LoggerViewModel.Log(message, LogLevel.Warning);
                    continue;
                }

                var hasPort  = false;
                var townName = townNameAttr.Value;

                if (!int.TryParse(numSlotsAttr.Value, out int numSlots))
                {
                    var message = $"MapDataConfig.ApplyConfigFile() - {TOWN_ATTRIBUTE_NUM_SLOTS} attribute has an invalid value. Aborting...";
                    LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                    return false;
                }

                if (hasPortAttr != null)
                {
                    hasPort = hasPortAttr.Value == "1";
                }

                regions[i] = new MapDataRegion(i + 1, townName, numSlots, hasPort);
            }

            return true;
        }

        private XmlNode CreateTownNode(string key, int numSlots, bool hasPort)
        {
            var townNode = _configDoc.CreateElement(TOWN_DEFINITION);

            var nameAttribute = _configDoc.CreateAttribute(TOWN_ATTRIBUTE_KEY);
            nameAttribute.Value = key;

            var slotsAttribute = _configDoc.CreateAttribute(TOWN_ATTRIBUTE_NUM_SLOTS);
            slotsAttribute.Value = numSlots.ToString();

            var hasPortAttribute = _configDoc.CreateAttribute(TOWN_ATTRIBUTE_HAS_PORT);
            hasPortAttribute.Value = hasPort ? "1" : "0";

            townNode.Attributes.Append(nameAttribute);
            townNode.Attributes.Append(slotsAttribute);
            townNode.Attributes.Append(hasPortAttribute);

            return townNode;
        }
    }
}
