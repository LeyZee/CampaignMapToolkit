using System;

namespace CAIME.Validators
{
    internal static class AttritionsValidator
    {
        public static bool Validate(Project project)
        {
            var isSuccess = true;
            var mapHexFile = project.MapHexFile;
            var attritionsCount = mapHexFile.Attritions.Count;

            if (attritionsCount != project.Database.CachedAttritions.Count)
            {
                LoggerViewModel.Log($"Attritions validation: There is a mismatch between database attritions (count = {project.Database.CachedAttritions.Count}) and map.hex attritions (count = {attritionsCount}).", LogLevel.Error);
                isSuccess = false;
            }

            for (int i = 0; i < attritionsCount; ++i)
            {
                var db_attrition = project.Database.CachedAttritions[i];
                var hex_attrition_key = mapHexFile.GetAttritionName(i);

                if (db_attrition.Key != hex_attrition_key)
                {
                    LoggerViewModel.Log($"Attritions validation: Attrition names mismatch. Attrition index was {i}. DB attrition key was {db_attrition.Key}. Map.hex attrition key was {hex_attrition_key}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            //Invalid attrition index (out of range)
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (hex.AttritionIndex != Hex.INVALID_ATTRITION_INDEX && hex.AttritionIndex >= attritionsCount)
                {
                    LoggerViewModel.Log($"Attritions validation: Hex({hex.Q}, {hex.R}) has invalid attrition index {hex.AttritionIndex}. Valid range is 0-{attritionsCount - 1}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            return isSuccess;
        }
    }
}
