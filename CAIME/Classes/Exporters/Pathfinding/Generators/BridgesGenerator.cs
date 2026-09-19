using System;
using System.Collections.Generic;
using System.IO;

namespace CAIME.Pathfinding
{
    using BridgePart = List<Hex>;

    internal class BridgeEdges
    {
        public BridgePart[] Parts;

        public BridgeEdges()
        {
            Parts = new BridgePart[] { new BridgePart(), new BridgePart() };
        }
    }

    internal class BridgesData
    {
        public List<BridgeEdges> BridgeEdges { get; private set; }
        public byte[] Data { get; private set; }

        public void Set(List<BridgeEdges> edges, byte[] data)
        {
            BridgeEdges = edges;
            Data        = data;
        }
    }

    internal class BridgesGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;
        private readonly bool       _bWriteDebugInfo;

        private int _width;
        private int _height;

        public BridgesGenerator(MapHexFile mapHexFile, string debugPath, bool bWriteDebug = false)
        {
            _mapHexFile         = mapHexFile;
            _debugPath          = debugPath;
            _bWriteDebugInfo    = bWriteDebug;
        }

        #region Step 1
        /// <summary>
        /// Finds all hexes of a single bridge (BFS search)
        /// </summary>
        /// <param name="startIndex">First bridge hex index</param>
        /// <param name="isVisited">Array that flags whether hex cell was already visited (optimises algorithm time)</param>
        /// <returns>A list of hex indices that make up a contiguous bridge.</returns>
        private List<int> LocateBridge(int startIndex, bool[] isVisited)
        {
            var bridge = new List<int>();

            var queue = new Queue<int>();
            queue.Enqueue(startIndex);
            bridge.Add(startIndex);
            isVisited[startIndex] = true;

            while (queue.Count > 0)
            {
                var hexIndex = queue.Dequeue();
                var hex = _mapHexFile.HexData[hexIndex];

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, _width, _height);
                    if (nbrIndex == -1)
                        continue;

                    if (isVisited[nbrIndex])
                        continue;

                    isVisited[nbrIndex] = true;

                    if (_mapHexFile.HexData[nbrIndex].IsBridge)
                    {
                        queue.Enqueue(nbrIndex);
                        bridge.Add(nbrIndex);
                    }
                }
            }

            return bridge;
        }

        /// <summary>
        /// Step 1. Locates all individual bridges on the hex map
        /// </summary>
        /// <returns>A list of hex indices per bridge</returns>
        private List<List<int>> LocateBridges()
        {
            var bridges   = new List<List<int>>();
            var isVisited = new bool[_mapHexFile.Capacity];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];

                if (isVisited[hexIndex])
                    continue;

                if (hex.IsBridge)
                {
                    bridges.Add(LocateBridge(hexIndex, isVisited));
                }
            }

            return bridges;
        }
        #endregion

        /// <summary>
        /// Flood-fills all land hexes and stamps each with a globally increasing visit number. Land
        /// components are seeded at their row-major-first hex, BFS-filled in neighbour order 0-5, and
        /// bridges are crossed so connected landmasses share one continuous visit order. This visit
        /// order is what makes each bridge's "first reached" side deterministic.
        /// </summary>
        private int[] ComputeLandFloodOrder()
        {
            var order = new int[_mapHexFile.Capacity];
            for (int i = 0; i < order.Length; ++i)
                order[i] = int.MaxValue;

            var visited      = new bool[_mapHexFile.Capacity]; // land hexes already flooded
            var bridgeWalked = new bool[_mapHexFile.Capacity]; // bridge hexes already crossed
            var frontier     = new Queue<int>();
            int counter      = 0;

            for (int seed = 0; seed < _mapHexFile.Capacity; ++seed)
            {
                if (visited[seed])
                    continue;

                var seedHex = _mapHexFile.HexData[seed];
                if (!(seedHex.IsPassableLand || seedHex.IsBridgeCliff))
                    continue;

                visited[seed] = true;
                order[seed]   = counter++;
                frontier.Enqueue(seed);

                while (frontier.Count > 0)
                {
                    var hex = _mapHexFile.HexData[frontier.Dequeue()];

                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, _width, _height);
                        if (nbrIndex == -1)
                            continue;

                        var neighbour = _mapHexFile.HexData[nbrIndex];

                        if (neighbour.IsPassableLand || neighbour.IsBridgeCliff)
                        {
                            if (visited[nbrIndex])
                                continue;

                            visited[nbrIndex] = true;
                            order[nbrIndex]   = counter++;
                            frontier.Enqueue(nbrIndex);
                        }
                        else if (neighbour.IsBridge && !bridgeWalked[nbrIndex])
                        {
                            // Walk the whole bridge and flood the coastal hexes on its far side into
                            // this same flood, so bridge-connected landmasses stay one component.
                            CrossBridge(nbrIndex, visited, bridgeWalked, order, frontier, ref counter);
                        }
                    }
                }
            }

            return order;
        }

        /// <summary>
        /// BFS-walks every hex of a single bridge (neighbour order 0-5) and floods the coastal hexes
        /// adjacent to it into the current flood, giving them the next visit numbers.
        /// </summary>
        private void CrossBridge(int startBridgeHex, bool[] visited, bool[] bridgeWalked,
                                 int[] order, Queue<int> frontier, ref int counter)
        {
            var bridgeQueue = new Queue<int>();
            bridgeQueue.Enqueue(startBridgeHex);
            bridgeWalked[startBridgeHex] = true;

            while (bridgeQueue.Count > 0)
            {
                var bridgeHex = _mapHexFile.HexData[bridgeQueue.Dequeue()];

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(bridgeHex, dir, _width, _height);
                    if (nbrIndex == -1)
                        continue;

                    var neighbour = _mapHexFile.HexData[nbrIndex];
                    if (neighbour.IsBridge)
                    {
                        if (!bridgeWalked[nbrIndex])
                        {
                            bridgeWalked[nbrIndex] = true;
                            bridgeQueue.Enqueue(nbrIndex);
                        }
                    }
                    else if ((neighbour.IsCoast || neighbour.IsBridgeCliff) && !visited[nbrIndex])
                    {
                        visited[nbrIndex] = true;
                        order[nbrIndex]   = counter++;
                        frontier.Enqueue(nbrIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the bridge's "seed" hex: the coastal (beach/cliff) hex adjacent to the bridge
        /// that the land flood reached first. Its side becomes the bridge's near side.
        /// </summary>
        private int FindSeedEdge(List<int> bridge, int[] floodOrder)
        {
            int best = -1;
            int bestOrder = int.MaxValue;

            foreach (int bridgeHexIndex in bridge)
            {
                var hex = _mapHexFile.HexData[bridgeHexIndex];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, _width, _height);
                    if (nbrIndex == -1)
                        continue;

                    if (_mapHexFile.HexData[nbrIndex].IsCoast && floodOrder[nbrIndex] < bestOrder)
                    {
                        bestOrder = floodOrder[nbrIndex];
                        best      = nbrIndex;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Collects the bridge's coastal edge hexes in discovery order: starting at the seed hex's
        /// bridge, BFS-walks the bridge hexes (neighbour order 0-5) and appends each coastal
        /// neighbour as it is found. The resulting order is what <see cref="SplitEdges"/> consumes.
        /// </summary>
        private List<int> BuildBeachWalk(int seedEdge)
        {
            var landBridge   = new List<int>();
            var inLandBridge = new HashSet<int>();

            // The walk is anchored at the seed's first bridge neighbour (neighbour order 0-5).
            var seedHex = _mapHexFile.HexData[seedEdge];
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                int nbrIndex = HexGridUtility.GetNeighbourIndexFast(seedHex, dir, _width, _height);
                if (nbrIndex != -1 && _mapHexFile.HexData[nbrIndex].IsBridge)
                {
                    landBridge.Add(nbrIndex);
                    inLandBridge.Add(nbrIndex);
                    break;
                }
            }

            var beach   = new List<int> { seedEdge };
            var inBeach = new HashSet<int> { seedEdge };

            for (int i = 0; i < landBridge.Count; ++i)
            {
                var bridgeHex = _mapHexFile.HexData[landBridge[i]];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(bridgeHex, dir, _width, _height);
                    if (nbrIndex == -1)
                        continue;

                    var neighbour = _mapHexFile.HexData[nbrIndex];
                    if (neighbour.IsBridge)
                    {
                        if (inLandBridge.Add(nbrIndex))
                            landBridge.Add(nbrIndex);
                    }
                    else if (neighbour.IsCoast)
                    {
                        if (inBeach.Add(nbrIndex))
                            beach.Add(nbrIndex);
                    }
                }
            }

            return beach;
        }

        /// <summary>
        /// Splits the edge-hex walk into the two bridge sides: the first side is seeded from the last
        /// hex of the walk (the far end) and grown by absorbing adjacent hexes scanned in reverse,
        /// and the hexes left over form the second (near) side. This fixes the side order (far side
        /// is <c>Parts[0]</c>) and the per-side hex order to match the game-ready .ppd.
        /// </summary>
        private BridgeEdges SplitEdges(List<int> beach)
        {
            var bridge = new BridgeEdges();
            if (beach.Count == 0)
                return bridge;

            var toClassify = beach.GetRange(0, beach.Count - 1);

            var first = new List<int> { beach[beach.Count - 1] };
            GrowEdge(first, toClassify);

            var second = new List<int>();
            if (toClassify.Count > 0)
            {
                second.Add(toClassify[toClassify.Count - 1]);
                toClassify.RemoveAt(toClassify.Count - 1);
                GrowEdge(second, toClassify);
            }

            foreach (int hexIndex in first)
                bridge.Parts[0].Add(_mapHexFile.HexData[hexIndex]);
            foreach (int hexIndex in second)
                bridge.Parts[1].Add(_mapHexFile.HexData[hexIndex]);

            return bridge;
        }

        /// <summary>
        /// Grows <paramref name="edge"/> by repeatedly absorbing the first hex (scanned in reverse)
        /// from <paramref name="pool"/> that is adjacent to a hex already in the edge, until no more
        /// can be absorbed. This is what separates one bridge side from the other.
        /// <para>A pool hex only becomes adjacent to the edge when one of its six neighbours is
        /// absorbed, so the candidates are tracked incrementally and the highest-positioned one is
        /// taken each round. That is the same hex the reverse scan would have found, without
        /// rescanning the pool after every absorption.</para>
        /// </summary>
        private void GrowEdge(List<int> edge, List<int> pool)
        {
            if (pool.Count == 0)
                return;

            var positionInPool = new Dictionary<int, int>(pool.Count);
            for (int position = 0; position < pool.Count; ++position)
                positionInPool[pool[position]] = position;

            var absorbed   = new bool[pool.Count];
            var candidates = new SortedSet<int>();

            foreach (int hexIndex in edge)
                AddAdjacentPoolHexes(hexIndex, positionInPool, absorbed, candidates);

            while (candidates.Count > 0)
            {
                int position = candidates.Max;
                candidates.Remove(position);

                absorbed[position] = true;
                edge.Add(pool[position]);

                AddAdjacentPoolHexes(pool[position], positionInPool, absorbed, candidates);
            }

            // The caller reads the leftovers back, so keep them in their original relative order.
            var remaining = new List<int>(pool.Count);
            for (int position = 0; position < pool.Count; ++position)
            {
                if (!absorbed[position])
                    remaining.Add(pool[position]);
            }

            pool.Clear();
            pool.AddRange(remaining);
        }

        /// <summary>
        /// Records every not-yet-absorbed pool hex neighbouring <paramref name="hexIndex"/> as a
        /// candidate for the next absorption.
        /// </summary>
        private void AddAdjacentPoolHexes(int hexIndex, Dictionary<int, int> positionInPool, bool[] absorbed, SortedSet<int> candidates)
        {
            var hex = _mapHexFile.HexData[hexIndex];
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, _width, _height);
                if (nbrIndex == -1)
                    continue;

                if (positionInPool.TryGetValue(nbrIndex, out int position) && !absorbed[position])
                    candidates.Add(position);
            }
        }

        /// <summary>
        /// Builds the two-sided edge grouping for every bridge. The order of bridges in the output
        /// is not significant (each hex references its bridge by an encoded index, not position).
        /// </summary>
        private List<BridgeEdges> CalculateBridgeEdges(List<List<int>> bridges, int[] floodOrder)
        {
            var bridgeEdges = new List<BridgeEdges>(bridges.Count);

            foreach (var bridgeHexes in bridges)
            {
                int seedEdge = FindSeedEdge(bridgeHexes, floodOrder);
                if (seedEdge == -1)
                {
                    // No coastal hex borders this bridge (degenerate); emit empty sides.
                    bridgeEdges.Add(new BridgeEdges());
                    continue;
                }

                var beach = BuildBeachWalk(seedEdge);
                bridgeEdges.Add(SplitEdges(beach));
            }

            return bridgeEdges;
        }

        public void Generate(out BridgesData bridgesData)
        {
            // Step 1. Locate all bridges (bridge hexes over sea hexes)
            // Step 2. Find land hexes that bridges connect to (namely bridge edges)
            // Step 3. Group found land hexes into two parts (one per bridge side)
            // Step 4. Encode bridges data to a byte array (7 bits for bridge index and 8th bit for bridge side index)

            _width  = (int)_mapHexFile.MapWidth;
            _height = (int)_mapHexFile.MapHeight;

            var bridges     = LocateBridges();
            var floodOrder  = ComputeLandFloodOrder();
            var bridgeEdges = CalculateBridgeEdges(bridges, floodOrder);


            if (bridgeEdges.Count > BridgeUtility.MAX_BRIDGES)
            {
                throw new OverflowException(
                    $"The map has {bridgeEdges.Count} bridges, but the pathfinding format can address at most {BridgeUtility.MAX_BRIDGES}. " +
                    "Remove the excess bridges before exporting.");
            }

            var data = new byte[_mapHexFile.Capacity];
            for (int bridgeIndex = 0; bridgeIndex < bridgeEdges.Count; ++bridgeIndex)
            {
                var edges = bridgeEdges[bridgeIndex];
                for (int sideIndex = 0; sideIndex < 2; ++sideIndex)
                {
                    var sideEdges = edges.Parts[sideIndex];
                    foreach (var hex in sideEdges)
                    {
                        int hexIndex   = HexGridUtility.IndexFromCoords(hex.R, hex.Q, (int)_mapHexFile.MapWidth);
                        data[hexIndex] = BridgeUtility.EncodeBridgeInfo(bridgeIndex, sideIndex);
                    }
                }
            }

            bridgesData = new BridgesData();
            bridgesData.Set(bridgeEdges, data);

#if DEBUG
            if (_bWriteDebugInfo)
            {
                var debugData = new byte[_mapHexFile.Capacity * 2];

                for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
                {
                    // Bridge hexes
                    debugData[hexIndex * 2 + 0] = _mapHexFile.HexData[hexIndex].IsBridge ? (byte)128 : (byte)0;
                    // Bridge edge hexes
                    debugData[hexIndex * 2 + 1] = data[hexIndex];
                }

                Utility.ArrayToRaw(_debugPath, "debug_bridges", debugData);
            }
#endif
        }
    }
}
