using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Pathfinding
{
    internal static class BridgeUtility
    {
        /// <summary>
        /// Most bridges the pathfinding format can address: the encoded value is bridgeIndex + 1
        /// in the low 7 bits of a byte, so index 126 (the 127th bridge) is the last one that fits.
        /// </summary>
        public const int MAX_BRIDGES = 127;

        /// <summary>
        /// Checks whether a land hex is a also bridge edge hex.
        /// </summary>
        /// <param name="hexIndex">Source hex index</param>
        public static bool IsBridgeCliff(BridgesData bridgesData, int hexIndex)
        {
            return bridgesData.Data[hexIndex] > 0;
        }

        /// <summary>
        /// Finds a random hex and it's index on the other side of the bridge
        /// </summary>
        /// <param name="hexIndex">Source hex index</param>
        public static int GetBridgeEdgeOnAnotherSide(MapHexFile mapHexFile, BridgesData bridgesData, int hexIndex)
        {
            DecodeBridgeInfo(bridgesData, hexIndex, out int bridgeIndex, out int sideIndex);

            var otherSideIndex = GetOtherBridgeSideIndex(sideIndex);
            var conHex = bridgesData.BridgeEdges[bridgeIndex].Parts[otherSideIndex].First();

            return HexGridUtility.IndexFromCoords(conHex.R, conHex.Q, (int)mapHexFile.MapWidth);
        }

        public static int GetOtherBridgeSideIndex(int sideIndex)
        {
            return (sideIndex + 1) % 2;
        }

        /// <summary>
        /// Decodes bridge hex info from <see cref="BridgesData.Data"/> format
        /// </summary>
        /// <param name="bridgesData"><see cref="BridgesGenerator"/> output</param>
        /// <param name="hexIndex">Bridge hex index</param>
        /// <param name="bridgeIndex">Decoded bridge index</param>
        /// <param name="sideIndex">Decoded bridge side index</param>
        public static void DecodeBridgeInfo(BridgesData bridgesData, int hexIndex, out int bridgeIndex, out int sideIndex)
        {
            var encodedBridgeInfo = bridgesData.Data[hexIndex];

            bridgeIndex = (encodedBridgeInfo & 127) - 1;
            sideIndex = encodedBridgeInfo >> 7;
        }

        /// <summary>
        /// Encodes bridge hex info to <see cref="BridgesData.Data"/> format
        /// <para>8th bit is bridge side index (either 0 or 1).</para>
        /// <para>Remaining 7 bits are bridge index to <see cref="bridgeEdges"/> list + 1</para>
        /// <para>(without +1 it will be impossible to detect bridge by index 0).</para>
        /// </summary>
        /// <param name="bridgeIndex">Bridge index to encode</param>
        /// <param name="sideIndex">Bridge side index to encode</param>
        /// <returns>Encoded value of bridge index combined with bridge side index</returns>
        public static byte EncodeBridgeInfo(int bridgeIndex, int sideIndex)
        {
            return (byte)((Bits.Pack(sideIndex, 1, "Bridge side index") << 7)
                        |  Bits.Pack(bridgeIndex + 1, 7, "Bridge index"));
        }
    }
}
