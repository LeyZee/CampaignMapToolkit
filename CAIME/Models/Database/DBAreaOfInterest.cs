using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Key = {Key}, Id = {Id}")]
    public class DBAreaOfInterest
    {
        public int      Id;
        public string   Key;
        public int      Colour;
    }
}
