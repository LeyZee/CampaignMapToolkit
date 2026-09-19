using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using CAIME;
using CAIME.Models;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Loads the game's <c>campaign_ground_types.xml</c> (the same db table the app reads) into a
    /// <see cref="DatabaseViewModel"/>, so a differential test can drive
    /// <c>MovementCostsGenerator</c> with the real game costs instead of hand-built stubs.
    ///
    /// <para>
    /// This mirrors <c>DatabaseViewModel.CacheGroundTypes</c>: every <c>&lt;campaign_ground_types&gt;</c>
    /// record contributes its <c>type</c> (key), <c>movement_cost</c> and <c>is_sea</c> flag, and land
    /// types are listed before sea types with sequential ids — the same ordering the app produces.
    /// </para>
    /// </summary>
    internal static class GroundTypesDb
    {
        public static DatabaseViewModel Load(string groundTypesXmlPath)
        {
            var doc  = XDocument.Load(groundTypesXmlPath);
            var land = new List<DBGroundType>();
            var sea  = new List<DBGroundType>();

            foreach (var record in doc.Root.Elements("campaign_ground_types"))
            {
                bool isSea = record.Element("is_sea")?.Value == "1";
                var  key   = record.Element("type")?.Value;
                int  cost  = int.Parse(record.Element("movement_cost").Value, CultureInfo.InvariantCulture);

                var entry = new DBGroundType { Key = key, MoveCost = cost, IsSea = isSea };
                (isSea ? sea : land).Add(entry);
            }

            var db = new DatabaseViewModel();
            db.CachedGroundTypes = new List<DBGroundType>(land.Count + sea.Count);
            db.CachedGroundTypes.AddRange(land);
            db.CachedGroundTypes.AddRange(sea);
            for (int i = 0; i < db.CachedGroundTypes.Count; ++i)
                db.CachedGroundTypes[i].Id = i;

            return db;
        }
    }
}
