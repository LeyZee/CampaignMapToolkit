using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding
{
    internal class Restriction
    {
        public List<Hex> Hexes;
        public List<byte> EdgeMasks;

        public Restriction()
        {
            Hexes = new List<Hex>();
            EdgeMasks = new List<byte>();
        }

        public void AddHex(Hex hex, byte mask)
        {
            Hexes.Add(hex);
            EdgeMasks.Add(mask);
        }
    }

    internal class RestrictionsGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        public RestrictionsGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        public void Generate(out Restriction[] restrictions)
        {
            restrictions = new Restriction[Hex.MAX_RESTRICTIONS_COUNT];
            for (int lvl = 0; lvl < Hex.MAX_RESTRICTIONS_COUNT; ++lvl)
            {
                restrictions[lvl] = new Restriction();
            }

            int width  = (int)_mapHexFile.MapWidth;
            int height = (int)_mapHexFile.MapHeight;

            // Pack the only neighbour-read field (RestrictionLvl) into a dense byte[] so the inner
            // loop reads from cache instead of dereferencing scattered Hex class objects.
            var restrictionLvls = new byte[_mapHexFile.Capacity];
            for (int i = 0; i < restrictionLvls.Length; ++i)
                restrictionLvls[i] = _mapHexFile.HexData[i].RestrictionLvl;

            // Reused across hexes (cleared each iteration) instead of allocating per hex.
            var masks = new byte[Hex.MAX_RESTRICTIONS_COUNT];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                var hex    = _mapHexFile.HexData[hexIndex];
                var hexLvl = restrictionLvls[hexIndex];
                Array.Clear(masks, 0, masks.Length);

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
                    if (nbrIndex == -1)
                        continue;

                    var nbrLvl = restrictionLvls[nbrIndex];
                    if (hexLvl < nbrLvl)
                    {
                        for (int lvl = hexLvl; lvl < nbrLvl; ++lvl)
                        {
                            masks[lvl] |= (byte)(1 << dir);
                        }
                    }
                }

                for (int lvl = 0; lvl < Hex.MAX_RESTRICTIONS_COUNT; ++lvl)
                {
                    if (masks[lvl] > 0)
                    {
                        restrictions[lvl].AddHex(hex, masks[lvl]);
                    }
                }
            }
        }
    }
}
