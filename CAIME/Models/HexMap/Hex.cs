using System;
using System.Diagnostics;
using SharpDX;

namespace CAIME
{
    [DebuggerDisplay("X = {Q}, Y = {R}, Index = {Index}, Type = {HexType}")]
    public class Hex : IEquatable<Hex>
    {
        public const int   INVALID_REGION_INDEX         = -1;
        public const sbyte INVALID_SLOT_INDEX           = -1;
        public const sbyte INVALID_ATTRITION_INDEX      = -1;
        public const sbyte INVALID_CLIMATE_INDEX        = -1;
        public const sbyte INVALID_GROUND_TYPE_INDEX    = -1;
        public const sbyte INVALID_AREA_OF_INT_INDEX    = -1;


        public const sbyte MAX_RESTRICTIONS_COUNT       = 16;
        public const sbyte MAX_SLOTS_COUNT              = 12;
        public const sbyte MAIN_SLOT_INDEX              = 0;
        public const sbyte PORT_SLOT_INDEX              = 1;

        public static Hex[,] Directions_FlatTop = new Hex[,]
        {
            { // even
                new Hex(0, 1),    // ↑ up
                new Hex(1, 0),    // ↗ up-right
                new Hex(1, -1),   // ↘ down-right
                new Hex(0, -1),   // ↓ down
                new Hex(-1, -1),  // ↙ down-left
                new Hex(-1, 0)    // ↖ up-left
            },
            { // odd
                new Hex(0, 1),    // ↑ up
                new Hex(1, 1),    // ↗ up-right
                new Hex(1, 0),    // ↘ down-right
                new Hex(0, -1),   // ↓ down
                new Hex(-1, 0),   // ↙ down-left
                new Hex(-1, 1)    // ↖ up-left
            }
        };

        /// <summary>
        /// Column
        /// </summary>
        public int     Q;
        /// <summary>
        /// Row
        /// </summary>
        public int     R;
        /// <summary>
        /// Hex Index
        /// </summary>
        public int     Index;

        public int              RegionId;
        public bool             IsBeach;
        public bool             IsPassable;
        public bool             IsBridge;
        public bool             IsRiver;
        public bool             IsRoad;
        public bool             IsTradeRoute;

        public sbyte            GroundTypeIndex;
        public sbyte            ClimateIndex;
        public sbyte            AttritionIndex;
        public sbyte            InterestIndex; // Area of Interest
        public byte             RestrictionLvl;

        public sbyte            TownSlotIndex;
        public bool             IsTownSprawl;

        public bool             IsSea;
        public bool             IsCliff;
        public bool             IsBorder;
        public bool             IsBridgeCliff;
        //public bool             IsTilemapCliff; //Used for the tilemap, but not for pathfinding or other map stuff

        public byte             TradeRouteMask;
        public byte             RoadEdgeMask;
        public byte             RiverEdgeMask;
        public byte             RegionEdgeMask;

        // Order in which this hex was painted as road/river (0 = loaded or never painted).
        // Used so triangle resolution follows paint order - see MapHexFile.CalculateRoadEdgeMasks.
        public int              RoadPaintSeq;
        public int              RiverPaintSeq;

        public bool             IsPassableLand          { get => IsPassable && IsLand; }
        public bool             IsPassableSea           { get => IsPassable && IsSea; }
        public bool             IsImpassable            { get => IsPassable == false; }
        public bool             IsLand                  { get => IsSea == false; }
        public bool             IsCoast                 { get => IsCliff || IsBeach; }

        public HexType          HexType                 { get; private set; }

        public Hex(int q, int r, int index = -1)
        {
            Q                   = q;
            R                   = r;
            Index               = index;

            RegionId            = INVALID_REGION_INDEX;
            GroundTypeIndex     = INVALID_GROUND_TYPE_INDEX;
            ClimateIndex        = INVALID_CLIMATE_INDEX;
            AttritionIndex      = INVALID_ATTRITION_INDEX;
            InterestIndex       = INVALID_AREA_OF_INT_INDEX;
            TownSlotIndex       = INVALID_SLOT_INDEX;
            RestrictionLvl      = 0;

            IsPassable          = true;
            IsBridge            = false;
            IsRiver             = false;
            IsBeach             = false;
            IsRoad              = false;
            IsTownSprawl        = false;
            IsTradeRoute        = false;

            IsSea               = false;
            IsCliff             = false;
            IsBorder            = false;
            IsBridgeCliff       = false;

            TradeRouteMask      = 0;
            RoadEdgeMask        = 0;
            RiverEdgeMask       = 0;
            RegionEdgeMask      = 0;
        }

        /// <summary>
        /// Creates a field-by-field copy of this hex. Hexes are mutable reference types,
        /// so storing one instance in several grid slots aliases them - clone instead.
        /// </summary>
        public Hex Clone()
        {
            return (Hex)this.MemberwiseClone();
        }

        public void UpdateHexType()
        {
            if (IsImpassable)
            {
                HexType = HexType.Impassable;
            }
            else
            if (IsBridgeCliff)
            {
                HexType = HexType.BridgeCliff;
            }
            else
            if (IsRiver)
            {
                HexType = HexType.River;
            }
            else
            if (IsCliff)
            {
                HexType = HexType.Impassable;
            }
            else
            if (IsBeach)
            {
                HexType = HexType.Beach;
            }
            else
            if (IsSea)
            {
                HexType = HexType.Sea;
            }
            else
            {
                HexType = HexType.Land;
            }
        }

        #region Equality
        /// <summary>
        /// Determines whether hexes are equal
        /// </summary>
        /// <param name="other"></param>
        /// <returns>A boolean value indicating whether hexes are equal</returns>
        public override bool Equals(object obj) {
            if (!(obj is Hex))
                return false;

            var other = (Hex)obj;
            return (this.Q == other.Q) && (this.R == other.R);
        }

        /// <summary>
        /// Get Hash code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            // Q + R would collide for every hex on the same anti-diagonal, which matters because
            // Hex is used as a Dictionary/HashSet key.
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        /// <summary>
        /// Determines whether hexes are equal
        /// </summary>
        /// <param name="other"></param>
        /// <returns>A boolean value indicating whether hexes are equal</returns>
        public bool Equals(Hex other)
        {
            if (other is null)
                return false;

            return (Q == other.Q) && (R == other.R);
        }

        /// <summary>
        /// Determines whether hexes are equal
        /// </summary>
        /// <param name="hex1">Left hex</param>
        /// <param name="hex2">Right hex</param>
        /// <returns></returns>
        public static bool operator ==(Hex hex1, Hex hex2)
        {
            if (hex1 is null && hex2 is null)
                return true;

            if (hex1 is null)
                return false;

            return hex1.Equals(hex2);
        }

        /// <summary>
        /// Determines whether hexes are not equal
        /// </summary>
        /// <param name="hex1">Left hex</param>
        /// <param name="hex2">Right hex</param>
        /// <returns></returns>
        public static bool operator !=(Hex hex1, Hex hex2)
        {
            if (hex1 is null && hex2 is null)
                return false;

            if (hex1 is null && !(hex2 is null))
                return true;

            return hex1.Equals(hex2) == false;
        }
        #endregion

        #region Coordinate Arithmetics
        /// <summary>
        /// Adds hex coordinates
        /// </summary>
        /// <param name="other">Hex to add</param>
        /// <returns>A summ of this and the other hexes</returns>
        public Hex Add(Hex other) => new Hex(this.Q + other.Q, this.R + other.R);

        /// <summary>
        /// Helper function to get distance between two hexes
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int CubeDistance(Vector3 a, Vector3 b)
        {
            return (int)(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z)) / 2;
        }

        /// <summary>
        /// Hexes to convert to Cube coordinates when hex.Q is even
        /// </summary>
        /// <param name="col"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public static Vector3 OffsetEvenQ_ToCube(int col, int row)
        {
            int x = col;
            int z = row - (col + (col & 1)) / 2;
            int y = -x - z;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Hexes to convert to Cube coordinates when hex.Q is odd
        /// </summary>
        /// <param name="col"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public static Vector3 OffsetOddQ_ToCube(int col, int row)
        {
            int x = col;
            int z = row - (col - (col & 1)) / 2;
            int y = -x - z;
            return new Vector3(x, y, z);
        }
        #endregion

        #region Distance
        /// <summary>
        /// Distance between two hexes holding odd-q offset coordinates. Offset deltas alone
        /// cannot express hex distance, so both hexes are converted to cube space first.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns>Distance between two hexes</returns>
        public int GetDistance(Hex hex)
        {
            var a = OffsetOddQ_ToCube(this.Q, this.R);
            var b = OffsetOddQ_ToCube(hex.Q, hex.R);
            return CubeDistance(a, b);
        }
        #endregion
    }

}
