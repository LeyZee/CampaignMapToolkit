using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// The distinct sets of region pairs a half-edge can be labelled with, each held once and addressed
    /// by the position it was first appended at. Position 0 is always the empty set.
    ///
    /// <para>
    /// A set is a sorted run of pair ordinals, so two sets are equal exactly when their contents match,
    /// whatever order their members were added in. Appending order is the only thing that decides what
    /// order road segments come out in, so it is never re-derived or re-sorted.
    /// </para>
    /// </summary>
    internal sealed class RegionPairSets
    {
        public const int Empty = 0;

        private readonly List<int[]>              _sets      = new List<int[]> { Array.Empty<int>() };
        private readonly Dictionary<Members, int> _positions = new Dictionary<Members, int>();
        private readonly Dictionary<long, int>    _unions    = new Dictionary<long, int>();

        public RegionPairSets()
        {
            _positions.Add(new Members(_sets[Empty]), Empty);
        }

        public int Count => _sets.Count;

        public int[] PairsIn(int set)
        {
            return _sets[set];
        }

        /// <summary>
        /// The position of the set holding everything <paramref name="set"/> holds plus
        /// <paramref name="pair"/>, appending it if this is the first time that set is needed.
        /// </summary>
        public int Union(int set, int pair)
        {
            long request = ((long)set << 32) | (uint)pair;
            if (_unions.TryGetValue(request, out int union))
                return union;

            var current = _sets[set];
            int slot    = Array.BinarySearch(current, pair);

            union = slot >= 0 ? set : PositionOf(WithPairAt(current, ~slot, pair));

            _unions.Add(request, union);
            return union;
        }

        private int PositionOf(int[] pairs)
        {
            var members = new Members(pairs);
            if (_positions.TryGetValue(members, out int existing))
                return existing;

            _positions.Add(members, _sets.Count);
            _sets.Add(pairs);

            return _sets.Count - 1;
        }

        private static int[] WithPairAt(int[] pairs, int slot, int pair)
        {
            var grown = new int[pairs.Length + 1];
            Array.Copy(pairs, 0, grown, 0, slot);
            grown[slot] = pair;
            Array.Copy(pairs, slot, grown, slot + 1, pairs.Length - slot);

            return grown;
        }

        private readonly struct Members : IEquatable<Members>
        {
            private readonly int[] _pairs;
            private readonly int   _hash;

            public Members(int[] pairs)
            {
                _pairs = pairs;

                int hash = 17;
                foreach (int pair in pairs)
                    hash = unchecked(hash * 31 ^ pair);

                _hash = hash;
            }

            public bool Equals(Members other)
            {
                if (_pairs.Length != other._pairs.Length)
                    return false;

                for (int i = 0; i < _pairs.Length; ++i)
                {
                    if (_pairs[i] != other._pairs[i])
                        return false;
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is Members other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _hash;
            }
        }
    }
}
