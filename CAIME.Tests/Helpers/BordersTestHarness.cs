using System;
using System.Reflection;
using CAIME;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Builds minimal <see cref="MapHexFile"/> instances for <c>BordersGenerator</c> tests.
    /// Uses reflection to bypass private setters, matching the pattern in
    /// <see cref="MovementTestHarness"/>.
    /// </summary>
    internal static class BordersTestHarness
    {
        internal static MapHexFile BuildMap(Hex[] hexData, uint width, uint height)
        {
            var map = new MapHexFile();
            SetProp(map, nameof(MapHexFile.MapWidth),  width);
            SetProp(map, nameof(MapHexFile.MapHeight), height);
            SetProp(map, nameof(MapHexFile.Capacity),  width * height);
            SetProp(map, nameof(MapHexFile.HexData),   hexData);
            return map;
        }

        private static void SetProp(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                throw new InvalidOperationException($"Property '{name}' not found on {target.GetType().Name}.");
            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter == null)
                throw new InvalidOperationException($"Property '{name}' has no setter.");
            setter.Invoke(target, new[] { value });
        }
    }
}
