using System.Globalization;
using System.Xml.Linq;
using CAIME;
using CAIME.Models;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Loads the game's <c>campaign_map_roads.xml</c> into <see cref="DatabaseViewModel.CachedRoads"/>,
    /// mirroring <c>DatabaseViewModel.CacheRoadCosts</c>: every <c>&lt;campaign_map_roads&gt;</c> record for
    /// the requested campaign contributes its <c>key</c>, <c>movement_cost</c> and <c>threshold</c>, in
    /// document order (road levels ascend, so the table's last entry is the top-level road).
    ///
    /// <para>
    /// <c>HeuristicCacheGenerator</c> reads <c>CachedRoads.Last().MoveCost</c> as the best-case cost of a
    /// road edge, so the heuristic cache cannot be reproduced byte-exactly without this table.
    /// </para>
    /// </summary>
    internal static class RoadsDb
    {
        public static void Load(DatabaseViewModel db, string roadsXmlPath, string campaignName)
        {
            var doc = XDocument.Load(roadsXmlPath);

            foreach (var record in doc.Root.Elements("campaign_map_roads"))
            {
                if (record.Element("campaign")?.Value != campaignName)
                    continue;

                db.CachedRoads.Add(new DBRoad
                {
                    Key          = record.Element("key")?.Value,
                    CampaignName = campaignName,
                    Threshold    = float.Parse(record.Element("threshold").Value, CultureInfo.InvariantCulture),
                    MoveCost     = (uint)int.Parse(record.Element("movement_cost").Value, CultureInfo.InvariantCulture),
                });
            }
        }
    }
}
