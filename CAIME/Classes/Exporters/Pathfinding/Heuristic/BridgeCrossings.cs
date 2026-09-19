using System.Collections.Generic;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// The one way a route may pass a cliff line: from a bridge-cliff hex movement reaches every hex on
    /// the opposite bank of the same bridge, whether or not those hexes are grid neighbours.
    /// </summary>
    internal sealed class BridgeCrossings
    {
        public BridgeCrossings(MapHexFile mapHexFile, BridgesData bridgesData)
        {
            OppositeSideByHex = new int[mapHexFile.Capacity][];

            var sides = SideHexIndices(mapHexFile, bridgesData);
            var hexes = mapHexFile.HexData;

            for (int hexIndex = 0; hexIndex < OppositeSideByHex.Length; ++hexIndex)
            {
                if (!hexes[hexIndex].IsBridgeCliff)
                    continue;

                BridgeUtility.DecodeBridgeInfo(bridgesData, hexIndex, out int bridgeIndex, out int sideIndex);
                if ((uint)bridgeIndex >= (uint)sides.Count)
                    continue;

                OppositeSideByHex[hexIndex] = sides[bridgeIndex][BridgeUtility.GetOtherBridgeSideIndex(sideIndex)];
            }
        }

        /// <summary>Per hex, the hexes reachable across its bridge, or null where there is no crossing.</summary>
        public int[][] OppositeSideByHex { get; }

        private static List<int[][]> SideHexIndices(MapHexFile mapHexFile, BridgesData bridgesData)
        {
            int width = (int)mapHexFile.MapWidth;
            var sides = new List<int[][]>(bridgesData.BridgeEdges.Count);

            foreach (var bridge in bridgesData.BridgeEdges)
            {
                var bridgeSides = new int[bridge.Parts.Length][];

                for (int sideIndex = 0; sideIndex < bridge.Parts.Length; ++sideIndex)
                {
                    var part      = bridge.Parts[sideIndex];
                    var hexIndices = new int[part.Count];

                    for (int i = 0; i < part.Count; ++i)
                        hexIndices[i] = HexGridUtility.IndexFromCoords(part[i].R, part[i].Q, width);

                    bridgeSides[sideIndex] = hexIndices;
                }

                sides.Add(bridgeSides);
            }

            return sides;
        }
    }
}
