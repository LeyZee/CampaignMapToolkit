using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CAIME;
using CAIME.Models;
using CAIME.Pathfinding;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Builds minimal, fully controlled <see cref="MapHexFile"/> / <see cref="DatabaseViewModel"/>
    /// instances and drives <see cref="MovementCostsGenerator"/> over them.
    ///
    /// <para>
    /// <see cref="MapHexFile"/> exposes the relevant state through <c>{ get; private set; }</c>
    /// auto-properties and offers no test seam, so the harness sets them through their private
    /// setters via reflection. This lets us exercise the real generator (and therefore every
    /// private cost/navigability branch it contains) against hand-built maps.
    /// </para>
    /// </summary>
    internal static class MovementTestHarness
    {
        // Ground-type indices used across the tests.
        // Land ground types occupy [0 .. LandGroundTypes.Count), sea ground types follow.
        public const sbyte GROUND_GRASS  = 0;   // land, cost 10
        public const sbyte GROUND_FOREST = 1;   // land, cost 30
        public const sbyte GROUND_RIVER  = 2;   // land, cost 40 (the "river" key CalcLandCost looks up)
        public const sbyte GROUND_OCEAN  = 3;   // sea,  cost 20
        public const sbyte GROUND_COAST  = 4;   // sea,  cost 50
        public const sbyte GROUND_VOID   = 5;   // sea,  cost 0  (for the zero-average case)

        public const ushort COST_GRASS  = 10;
        public const ushort COST_FOREST = 30;
        public const ushort COST_RIVER  = 40;
        public const ushort COST_OCEAN  = 20;
        public const ushort COST_COAST  = 50;
        public const ushort COST_VOID   = 0;

        private static readonly string _debugDir = CreateDebugDir();

        private static string CreateDebugDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "caime_mc_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            // The generator's debug code path concatenates "{path}{filename}.raw",
            // so the path must end with a separator.
            return dir + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Runs the generator over <paramref name="hexData"/> laid out on a
        /// <paramref name="width"/> x <paramref name="height"/> grid.
        /// </summary>
        /// <returns>The raw 8-bytes-per-hex edges array and the resulting move-cost table.</returns>
        public static (byte[] edges, List<ushort> costs) Run(Hex[] hexData, uint width, uint height)
        {
            var map = BuildMap(hexData, width, height);
            var db  = BuildDatabase();

            var generator = new MovementCostsGenerator(map, db, _debugDir);

            var edges = new byte[hexData.Length * 8];
            generator.Generate(ref edges);

            return (edges, generator.GetMoveCosts());
        }

        /// <summary>
        /// Convenience for the most common scenario: two vertically adjacent hexes
        /// A=(0,0) and B=(0,1) on a 1x2 grid. With flat-top direction 0 == (0,+1),
        /// edge A-&gt;B lives at index 0 and edge B-&gt;A (direction 3) at index 8+3.
        /// </summary>
        public static (byte aToB, byte bToA, List<ushort> costs) RunPair(Hex a, Hex b)
        {
            a.Index = 0;
            b.Index = 1;
            var (edges, costs) = Run(new[] { a, b }, 1, 2);
            return (edges[0 * 8 + 0], edges[1 * 8 + 3], costs);
        }

        public static Hex NewHex(int q, int r, int index, sbyte ground)
        {
            var hex = new Hex(q, r, index) { GroundTypeIndex = ground };
            return hex;
        }

        /// <summary>Builds a configured <see cref="MapHexFile"/> without running the generator.</summary>
        public static MapHexFile BuildMapFor(Hex[] hexData, uint width, uint height)
            => BuildMap(hexData, width, height);

        /// <summary>Creates a generator bound to the shared test database for direct invocation.</summary>
        public static MovementCostsGenerator CreateGenerator(MapHexFile map)
            => new MovementCostsGenerator(map, BuildDatabase(), _debugDir);

        private static MapHexFile BuildMap(Hex[] hexData, uint width, uint height)
        {
            var map = new MapHexFile();

            SetProperty(map, nameof(MapHexFile.MapWidth),  width);
            SetProperty(map, nameof(MapHexFile.MapHeight), height);
            SetProperty(map, nameof(MapHexFile.Capacity),  width * height);
            SetProperty(map, nameof(MapHexFile.HexData),   hexData);

            SetProperty(map, nameof(MapHexFile.LandGroundTypes),
                new List<string> { "grassland", "forest", "river" });
            SetProperty(map, nameof(MapHexFile.SeaGroundTypes),
                new List<string> { "ocean", "coast", "void" });

            return map;
        }

        private static DatabaseViewModel BuildDatabase()
        {
            var db = new DatabaseViewModel();
            db.CachedGroundTypes = new List<DBGroundType>
            {
                new DBGroundType { Key = "grassland", MoveCost = COST_GRASS,  IsSea = false },
                new DBGroundType { Key = "forest",    MoveCost = COST_FOREST, IsSea = false },
                new DBGroundType { Key = "river",     MoveCost = COST_RIVER,  IsSea = false },
                new DBGroundType { Key = "ocean",     MoveCost = COST_OCEAN,  IsSea = true  },
                new DBGroundType { Key = "coast",     MoveCost = COST_COAST,  IsSea = true  },
                new DBGroundType { Key = "void",      MoveCost = COST_VOID,   IsSea = true  },
            };
            return db;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            if (prop == null)
                throw new InvalidOperationException($"Property '{propertyName}' not found on {target.GetType().Name}.");

            var setter = prop.GetSetMethod(nonPublic: true);
            if (setter == null)
                throw new InvalidOperationException($"Property '{propertyName}' has no setter.");

            setter.Invoke(target, new[] { value });
        }
    }

    /// <summary>
    /// Decodes a single edge byte produced by the generator:
    /// bit 7 is navigability, bits 0..6 are the move-cost index.
    /// </summary>
    internal readonly struct Edge
    {
        public readonly bool Navigable;
        public readonly int  CostIndex;
        private readonly List<ushort> _costs;

        public Edge(byte raw, List<ushort> costs)
        {
            Navigable = (raw & 0b1000_0000) != 0;
            CostIndex = raw & 0b0111_1111;
            _costs    = costs;
        }

        public ushort CostValue => _costs[CostIndex];
    }
}
