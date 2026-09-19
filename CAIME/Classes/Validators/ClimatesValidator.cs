using System;

namespace CAIME.Validators
{
    internal static class ClimatesValidator
    {
        public static bool Validate(Project project)
        {
            var isSuccess = true;
            var mapHexFile = project.MapHexFile;
            var climatesCount = mapHexFile.Climates.Count;

            //Database/map.hex count mismatch
            if (climatesCount != project.Database.CachedClimates.Count)
            {
                LoggerViewModel.Log($"Climates validation: There is a mismatch between database climates (count = {project.Database.CachedClimates.Count}) and map.hex climates (count = {climatesCount}).", LogLevel.Error);
                isSuccess = false;
            }

            //Climate name mismatches with database
            for (int i = 0; i < climatesCount; ++i)
            {
                var db_climate = project.Database.CachedClimates[i];
                var hex_climate_key = mapHexFile.Climates[i];

                if (db_climate.Key != hex_climate_key)
                {
                    LoggerViewModel.Log($"Climates validation: Climate names mismatch. Climate index was {i}. DB climate key was {db_climate.Key}. Map.hex climate key was {hex_climate_key}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            //Invalid climate index (out of range)
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (hex.ClimateIndex == Hex.INVALID_CLIMATE_INDEX)
                {
                    LoggerViewModel.Log($"Climates validation: Hex({hex.Q}, {hex.R}) has no climate set.", LogLevel.Warning);
                    isSuccess = false;
                }
                else if (hex.ClimateIndex >= climatesCount)
                {
                    LoggerViewModel.Log($"Climates validation: Hex({hex.Q}, {hex.R}) has invalid climate index {hex.ClimateIndex}. Valid range is 0-{climatesCount - 1}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            return isSuccess;
        }
    }
}
