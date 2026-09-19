using System;
using System.Diagnostics;

namespace CAIME.Models
{
    [DebuggerDisplay("Campaign Name = {CampaignName}, Map Name = {CampaignMapName}")]
    public class DBCampaign
    {
        public string   CampaignName;
        public string   CampaignMapName;
        public bool     IsTutorial;
    }
}
