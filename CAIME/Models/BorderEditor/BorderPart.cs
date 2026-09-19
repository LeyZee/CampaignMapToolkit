using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAIME
{
    public class BorderPart
    {
        public List<BorderPoint> BorderPoints;
        public int regFrom;
        public int regTo;

        public BorderPart(int regionFrom, int regionTo)
        {
            regFrom = regionFrom;
            regTo = regionTo;
            BorderPoints = new List<BorderPoint>();
        }
    }
}
