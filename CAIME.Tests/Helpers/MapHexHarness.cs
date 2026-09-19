using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CAIME;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Builds hand-controlled <see cref="MapHexFile"/> instances and manages the throwaway
    /// directories the file-level tests write into. <see cref="MapHexFile"/> exposes its state
    /// through private setters and offers no test seam, so in-memory maps are assembled by driving
    /// those through reflection - the approach <see cref="MovementTestHarness"/> already takes.
    /// </summary>
    internal static class MapHexHarness
    {
        /// <summary>
        /// Clears the process-wide edit-tracking state <see cref="MapHexFile"/> keeps in static
        /// fields. These leak between tests, so every test touching them must call this from
        /// <c>[TestInitialize]</c>.
        /// </summary>
        public static void ResetStaticMaskState()
        {
            MapHexFile.RoadMasksNeedRecalculation   = false;
            MapHexFile.RiverMasksNeedRecalculation  = false;
            MapHexFile.RegionMasksNeedRecalculation = false;
            MapHexFile.TradeMasksNeedRecalculation  = false;

            MapHexFile.RoadDirtyHexes.Clear();
            MapHexFile.RiverDirtyHexes.Clear();
            MapHexFile.RegionDirtyHexes.Clear();
            MapHexFile.TradeDirtyHexes.Clear();

            MapHexFile.RoadPaintCounter  = 0;
            MapHexFile.RiverPaintCounter = 0;
        }

        /// <summary>
        /// Builds an in-memory map laid out row-major (index = row * width + col), with the
        /// region/ground-type/climate/attrition lists populated and no file involved.
        /// </summary>
        public static MapHexFile BuildGrid(uint width, uint height, Action<Hex> initHex = null)
        {
            var capacity = width * height;
            var hexData  = new Hex[capacity];

            for (int index = 0; index < capacity; ++index)
            {
                HexGridUtility.CoordsFromIndex(index, (int)width, out int row, out int col);

                var hex = new Hex(col, row, index)
                {
                    RegionId        = Hex.INVALID_REGION_INDEX,
                    GroundTypeIndex = Hex.INVALID_GROUND_TYPE_INDEX,
                    ClimateIndex    = Hex.INVALID_CLIMATE_INDEX,
                    AttritionIndex  = Hex.INVALID_ATTRITION_INDEX,
                    InterestIndex   = Hex.INVALID_AREA_OF_INT_INDEX,
                    TownSlotIndex   = Hex.INVALID_SLOT_INDEX,
                    IsPassable      = true,
                };

                initHex?.Invoke(hex);
                hexData[index] = hex;
            }

            var map = new MapHexFile();

            SetProperty(map, nameof(MapHexFile.MapWidth),  width);
            SetProperty(map, nameof(MapHexFile.MapHeight), height);
            SetProperty(map, nameof(MapHexFile.Capacity),  capacity);
            SetProperty(map, nameof(MapHexFile.HexData),   hexData);

            SetProperty(map, nameof(MapHexFile.LandRegions),     new List<string> { "land_a", "land_b" });
            SetProperty(map, nameof(MapHexFile.SeaRegions),      new List<string> { "sea_a" });
            SetProperty(map, nameof(MapHexFile.LandGroundTypes), new List<string> { "grassland", "forest" });
            SetProperty(map, nameof(MapHexFile.SeaGroundTypes),  new List<string> { "ocean" });
            SetProperty(map, nameof(MapHexFile.Climates),        new List<string> { "temperate" });
            SetProperty(map, nameof(MapHexFile.Attritions),      new List<string> { "none" });
            SetProperty(map, nameof(MapHexFile.AreasOfInterest), new List<string> { "none" });
            SetProperty(map, nameof(MapHexFile.GameName),        "warhammer3");
            SetProperty(map, nameof(MapHexFile.CampaignMapName), "test_map");

            return map;
        }

        public static void SetProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");

            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter == null)
                throw new InvalidOperationException($"Property '{propertyName}' has no setter.");

            setter.Invoke(target, new[] { value });
        }

        public static object InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            if (method == null)
                throw new InvalidOperationException($"Method '{methodName}' not found on {target.GetType().Name}.");

            return method.Invoke(target, args);
        }

        public static object InvokePrivateStatic(Type type, string methodName, params object[] args)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException($"Static method '{methodName}' not found on {type.Name}.");

            return method.Invoke(null, args);
        }

        public static object GetPrivateField(object target, string fieldName)
        {
            var field = FindField(target.GetType(), fieldName);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");

            return field.GetValue(target);
        }

        public static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = FindField(target.GetType(), fieldName);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");

            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    return field;
            }

            return null;
        }

        public static string CreateTempDir(string prefix)
        {
            var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteTempDir(string dir)
        {
            try
            {
                if (dir != null && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
