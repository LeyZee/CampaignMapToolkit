using System;
using System.Windows;

namespace CAIME
{
    public partial class MapHexEditor
    {
        public enum ChangeOperation
        {
            Create,
            Delete,
            Rename
        }

        public MapHexFile   MapHexFile  { get; private set; }
        public ColourTable  ColourTable { get; private set; }

        public MapHexEditor(MapHexFile mapHexFile, ColourTable colourTable)
        {
            MapHexFile  = mapHexFile;
            ColourTable = colourTable;
        }
    }
}
