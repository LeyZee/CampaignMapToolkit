using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Key = {Key}, CampaignName = {CampaignName}, MoveCost = {MoveCost}")]
    public class DBRoad
    {
        public string   Key;
        public string   CampaignName;
        public float    Threshold;
        public uint     MoveCost;
    }
}
