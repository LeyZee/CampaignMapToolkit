using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Key: {Key}, Id: {Id}, IsSea: {IsSea}")]
    public class DBRegion
    {
        public int      Id;
        public int      Colour;
        public bool     IsSea;
        public string   Key;
    }
}
