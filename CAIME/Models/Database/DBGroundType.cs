using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Key = {Key}, Id = {Id}, IsSea = {IsSea}, Cost = {MoveCost}")]
    public class DBGroundType
    {
        public int      Id;
        public string   Key;
        public int      MoveCost;
        public bool     IsSea;
        public int      Colour;
    }
}
