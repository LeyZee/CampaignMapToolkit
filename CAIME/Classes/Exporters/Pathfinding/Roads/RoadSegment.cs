using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding
{
    /// <summary>
    /// A stretch of road together with the settlement-to-settlement connections it serves. This is the
    /// contract between the road generator and <c>PathfindingExporter.WriteRoads</c>: the order of the
    /// segments, of each group's region pairs and of each road's hexes is all observable in the
    /// ROAD_SEGMENTS section. See <c>Docs/road_segments_ppd_format.md</c>.
    /// </summary>
    internal sealed class RoadSegment
    {
        public RouteGroup RouteGroup;
        public Road Road;
    }

    /// <summary>The region pairs a road segment serves, in ascending order.</summary>
    internal sealed class RouteGroup
    {
        public readonly List<RegionsPair> RegionPairs = new List<RegionsPair>();
    }

    internal struct RegionsPair : IEquatable<RegionsPair>, IComparable<RegionsPair>
    {
        public int SrcRegionIndex;
        public int DstRegionIndex;

        public RegionsPair(int src, int dst)
        {
            SrcRegionIndex = src;
            DstRegionIndex = dst;
        }

        public bool Equals(RegionsPair other)
        {
            return SrcRegionIndex == other.SrcRegionIndex &&
                   DstRegionIndex == other.DstRegionIndex;
        }

        public int CompareTo(RegionsPair other)
        {
            int bySrc = SrcRegionIndex.CompareTo(other.SrcRegionIndex);
            return bySrc != 0 ? bySrc : DstRegionIndex.CompareTo(other.DstRegionIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is RegionsPair pair && Equals(pair);
        }

        public override int GetHashCode()
        {
            return (SrcRegionIndex * 397) ^ DstRegionIndex;
        }
    }

    /// <summary>The hexes a road segment passes through, in the order they were first reached.</summary>
    internal sealed class Road
    {
        public readonly List<RoadHex> Hexes = new List<RoadHex>();

        private readonly Dictionary<int, int> _positionOfHex = new Dictionary<int, int>();

        public void AddOrUpdateHex(int hexIndex, sbyte edgeBit)
        {
            if (_positionOfHex.TryGetValue(hexIndex, out int position))
            {
                Hexes[position].EdgeMask |= edgeBit;
                return;
            }

            _positionOfHex[hexIndex] = Hexes.Count;
            Hexes.Add(new RoadHex(hexIndex, edgeBit));
        }
    }

    /// <summary>One hex's contribution to a road: which of its six edges the road runs along.</summary>
    internal sealed class RoadHex
    {
        public int HexIndex;
        public sbyte EdgeMask;

        public RoadHex(int hexIndex, sbyte edgeMask)
        {
            HexIndex = hexIndex;
            EdgeMask = edgeMask;
        }
    }
}
