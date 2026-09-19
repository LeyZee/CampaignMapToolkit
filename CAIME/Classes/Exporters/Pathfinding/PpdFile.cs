using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CAIME.Pathfinding
{
    /// <summary>
    /// Reader for a complete pathfinding .ppd file, following
    /// 010_hex_editor_pathfinding_ppd_template.bt and PathfindingExporter.WriteProcessedData.
    ///
    /// <para>
    /// Every top-level section is parsed in file order and its <em>raw</em> byte slice is
    /// retained (see the <c>*Raw</c> properties). This lets callers serialize a single
    /// generator's output to game-ready bytes and compare it against the exact same section
    /// lifted out of a real .ppd, without re-deriving offsets at the call site.
    /// </para>
    /// </summary>
    public sealed class PpdFile
    {
        public const ulong  MagicNumber = 0x0A1A0A0D44505089;
        public const uint   Version     = 2;

        public ulong    Magic;
        public uint     FileVersion;
        public int      LandRegionsCount;
        public string[] LandRegionNames;
        public int      Width;
        public int      Height;

        /// <summary>Raw HEX_CELL_DATA blob: Width * Height cells, 8 bytes each.</summary>
        public byte[]   CellData;

        /// <summary>The MovementCosts table (cost indices in edges point into this).</summary>
        public ushort[] MoveCosts;

        /// <summary>Number of tile groups (drives the heuristic-cache and cumulative-border sizes).</summary>
        public int      TileGroupsCount;

        // --- Raw section slices, exactly as written by PathfindingExporter ---------

        /// <summary>TILE_GROUPS_ARRAY: <c>uint16 count</c> followed by <c>count</c> TILE_GROUP entries (4 bytes each: hlci, region).</summary>
        public byte[]   TileGroupsRaw;

        /// <summary>
        /// BEACHES_ARRAY: <c>uint16 connectionCount</c> followed by one HLCI_BEACH_DATA per
        /// connection (a (enter, leave) HLCI pair plus its land-to-sea and sea-to-land hex lists).
        /// </summary>
        public byte[]   BeachesRaw;

        /// <summary>HLCI_CONNECTIONS: <c>uint16 count</c> followed by <c>count</c> HLCI pairs (4 bytes each).</summary>
        public byte[]   HlciConnectionsRaw;

        /// <summary>BRIDGES_ARRAY: <c>uint16 count</c> followed by the bridge cell data.</summary>
        public byte[]   BridgesRaw;

        /// <summary>HEURISTIC_CACHE: <c>TileGroupsCount^2</c> uint32 distances (row-major), no count prefix.</summary>
        public byte[]   HeuristicCacheRaw;

        /// <summary>PASSABLE_BORDER_HEXES: <c>uint32 count</c> followed by the border hexes.</summary>
        public byte[]   PassableBorderHexesRaw;

        /// <summary>CUMULATIVE_BORDER_HEXES: one <c>uint32</c> per tile group (the running border-hex offset), no count prefix.</summary>
        public byte[]   CumulativeBorderHexesRaw;

        /// <summary>
        /// ROAD_SEGMENTS: <c>uint16 count</c> followed by one ROAD_SEGMENT per segment — an
        /// <c>int32</c> direction count plus that many SEGMENT_DIRECTION region pairs (4 bytes each),
        /// then a <c>uint16</c> hex count plus that many HEX_WITH_EDGE_MASK entries (5 bytes each).
        /// </summary>
        public byte[]   RoadSegmentsRaw;

        /// <summary>
        /// RESTRICTION_LEVELS: <c>byte levelCount</c> followed by the per-level hex lists.
        /// Null when the file has no restrictions section (older map versions).
        /// </summary>
        public byte[]   RestrictionsRaw;

        public bool HasRestrictions => RestrictionsRaw != null;

        public int CellCount => Width * Height;

        /// <summary>Edge byte for hex <paramref name="hexIndex"/> in direction <paramref name="dir"/> (0..5).</summary>
        public byte EdgeByte(int hexIndex, int dir) => CellData[hexIndex * 8 + dir];

        /// <summary>HexType nibble (high 4 bits of cell byte 7) for hex <paramref name="hexIndex"/>.</summary>
        public byte HexTypeNibble(int hexIndex) => (byte)(CellData[hexIndex * 8 + 7] >> 4);

        public static PpdFile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ppd   = new PpdFile();

            using (var ms = new MemoryStream(bytes, writable: false))
            using (var br = new BinaryReader(ms))
            {
                // PPD_HEADER
                ppd.Magic            = br.ReadUInt64();
                ppd.FileVersion      = br.ReadUInt32();
                ppd.LandRegionsCount = br.ReadInt32();

                // LAND_REGIONS_ARRAY
                ppd.LandRegionNames = new string[ppd.LandRegionsCount];
                for (int i = 0; i < ppd.LandRegionsCount; ++i)
                {
                    int nameLen = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(nameLen);
                    ppd.LandRegionNames[i] = Encoding.ASCII.GetString(nameBytes);
                }

                // HEX_MAP_DATA
                ppd.Width    = br.ReadUInt16();
                ppd.Height   = br.ReadUInt16();
                ppd.CellData = br.ReadBytes(ppd.CellCount * 8);

                // MOVEMENT_COSTS_ARRAY
                int moveCostsCount = br.ReadInt32();
                ppd.MoveCosts = new ushort[moveCostsCount];
                for (int i = 0; i < moveCostsCount; ++i)
                    ppd.MoveCosts[i] = br.ReadUInt16();

                // TILE_GROUPS_ARRAY (captured)
                ppd.TileGroupsRaw = ReadSection(bytes, br, () =>
                {
                    ppd.TileGroupsCount = br.ReadUInt16();
                    Skip(br, ppd.TileGroupsCount * 4); // TILE_GROUP[count], 4 bytes each
                });

                // PASSABLE_HEX_EDGES_ARRAY (one PASSABLE_HEX_EDGE per land region)
                for (int r = 0; r < ppd.LandRegionsCount; ++r)
                {
                    int hexes = br.ReadUInt16();
                    Skip(br, hexes * 5); // HEX_WITH_EDGE_MASK, 5 bytes each
                }

                // BEACHES_ARRAY (captured)
                ppd.BeachesRaw = ReadSection(bytes, br, () =>
                {
                    int beachConnections = br.ReadUInt16();
                    for (int i = 0; i < beachConnections; ++i)
                    {
                        Skip(br, 4); // HLCI pair (enter, leave)
                        int landToSea = br.ReadUInt16();
                        Skip(br, landToSea * 5);
                        int seaToLand = br.ReadUInt16();
                        Skip(br, seaToLand * 5);
                    }
                });

                // HLCI_CONNECTIONS (captured)
                ppd.HlciConnectionsRaw = ReadSection(bytes, br, () =>
                {
                    int hlciConnections = br.ReadUInt16();
                    Skip(br, hlciConnections * 4);
                });

                // BRIDGES_ARRAY (captured)
                ppd.BridgesRaw = ReadSection(bytes, br, () =>
                {
                    int bridges = br.ReadUInt16();
                    for (int i = 0; i < bridges; ++i)
                    {
                        int side1 = br.ReadUInt16();
                        Skip(br, side1 * 4);
                        int side2 = br.ReadUInt16();
                        Skip(br, side2 * 4);
                    }
                });

                // HEURISTIC_CACHE: TileGroupsCount^2 uint32 distances (captured)
                ppd.HeuristicCacheRaw = ReadSection(bytes, br, () =>
                {
                    Skip(br, (long)ppd.TileGroupsCount * ppd.TileGroupsCount * 4);
                });

                // PASSABLE_BORDER_HEXES (captured)
                ppd.PassableBorderHexesRaw = ReadSection(bytes, br, () =>
                {
                    int borderHexes = br.ReadInt32();
                    Skip(br, borderHexes * 4); // HEX, 4 bytes each
                });

                // CUMULATIVE_BORDER_HEXES: one uint32 per tile group (captured)
                ppd.CumulativeBorderHexesRaw = ReadSection(bytes, br, () =>
                {
                    Skip(br, (long)ppd.TileGroupsCount * 4);
                });

                // ROAD_SEGMENTS (captured)
                ppd.RoadSegmentsRaw = ReadSection(bytes, br, () =>
                {
                    int roadSegments = br.ReadUInt16();
                    for (int i = 0; i < roadSegments; ++i)
                    {
                        int directions = br.ReadInt32();
                        Skip(br, directions * 4); // SEGMENT_DIRECTION, 4 bytes each
                        int hexes = br.ReadUInt16();
                        Skip(br, hexes * 5);       // HEX_WITH_EDGE_MASK, 5 bytes each
                    }
                });

                // RESTRICTION_LEVELS (optional) — present only when more than the trailing
                // CRC32 checksum remains in the file.
                long remaining = ms.Length - ms.Position;
                if (remaining > 4)
                {
                    ppd.RestrictionsRaw = ReadSection(bytes, br, () =>
                    {
                        int levels = br.ReadByte();
                        for (int i = 0; i < levels; ++i)
                        {
                            int hexes = br.ReadInt32();
                            Skip(br, hexes * 5);
                        }
                    });
                }

                // CRC32_CHECKSUM (4 bytes) trails the data — not validated here.
            }

            return ppd;
        }

        private static void Skip(BinaryReader br, long count)
        {
            br.BaseStream.Seek(count, SeekOrigin.Current);
        }

        /// <summary>
        /// Runs <paramref name="parse"/> over the reader and returns the raw bytes it consumed.
        /// </summary>
        private static byte[] ReadSection(byte[] source, BinaryReader br, Action parse)
        {
            long start = br.BaseStream.Position;
            parse();
            long end = br.BaseStream.Position;

            var slice = new byte[end - start];
            Array.Copy(source, start, slice, 0, slice.Length);
            return slice;
        }
    }
}
