using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Region: {Region}, Province: {Province}")]
    public class DBRegionToProvince
    {
        public string   Region;
        public string   Province;
    }
}
