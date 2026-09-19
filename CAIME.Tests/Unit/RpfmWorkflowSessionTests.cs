using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CAIME.Rpfm;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="RpfmWorkflowSession"/>'s table resolution. A session layers the
    /// project's modded packs over the vanilla pack and merges every fragment each contributes; the
    /// merge is what decides which database rows the exporters ultimately see, and getting it wrong
    /// silently exports a map built against the wrong tables.
    ///
    /// <para>
    /// <c>MatchTables</c> is private and static and takes a private nested type, so it is reached
    /// through reflection - the rest of the session needs a real RPFM installation and pack files.
    /// </para>
    /// </summary>
    [TestClass]
    public class RpfmWorkflowSessionTests
    {
        private static readonly Type SessionType     = typeof(RpfmWorkflowSession);
        private static readonly Type TableSourceType = SessionType.GetNestedType("TableSource", BindingFlags.NonPublic);

        [TestMethod]
        public void MatchTables_PicksUpEveryFragmentUnderATablesFolder()
        {
            var resolved = NewResolvedDictionary();

            var matched = MatchTables(
                tables: new[] { "campaign_regions" },
                packEntries: new[]
                {
                    "db/campaign_regions_tables/fragment_a",
                    "db/campaign_regions_tables/fragment_b",
                    "db/campaign_map_roads_tables/roads",
                    "text/localisation.loc",
                },
                sourcePackPath: @"C:\packs\mod.pack",
                resolved: resolved);

            CollectionAssert.AreEqual(new[] { "campaign_regions" }, matched.ToArray(), "Matched tables");
            CollectionAssert.AreEqual(
                new[] { "db/campaign_regions_tables/fragment_a", "db/campaign_regions_tables/fragment_b" },
                FragmentEntries(resolved, "campaign_regions").ToArray(),
                "Fragments recorded for the table");
        }

        [TestMethod]
        public void MatchTables_TableAbsentFromThePack_IsNotResolved()
        {
            var resolved = NewResolvedDictionary();

            var matched = MatchTables(
                tables: new[] { "campaign_regions" },
                packEntries: new[] { "db/campaign_map_roads_tables/roads" },
                sourcePackPath: @"C:\packs\mod.pack",
                resolved: resolved);

            Assert.AreEqual(0, matched.Count, "Nothing should have matched.");
            Assert.AreEqual(0, resolved.Count, "An unmatched table must not be added to the resolved set.");
        }

        // A second pack adds to the fragments already recorded rather than replacing them: the
        // workflow merges every pack's contribution instead of letting one pack win outright.
        [TestMethod]
        public void MatchTables_SecondPack_AppendsToTheFirstPacksFragments()
        {
            var resolved = NewResolvedDictionary();

            MatchTables(
                tables: new[] { "campaign_regions" },
                packEntries: new[] { "db/campaign_regions_tables/from_mod" },
                sourcePackPath: @"C:\packs\mod.pack",
                resolved: resolved);

            MatchTables(
                tables: new[] { "campaign_regions" },
                packEntries: new[] { "db/campaign_regions_tables/from_vanilla" },
                sourcePackPath: @"C:\packs\vanilla.pack",
                resolved: resolved);

            Assert.AreEqual(1, resolved.Count, "Both packs contribute to one table entry.");
            CollectionAssert.AreEqual(
                new[] { "db/campaign_regions_tables/from_mod", "db/campaign_regions_tables/from_vanilla" },
                FragmentEntries(resolved, "campaign_regions").ToArray(),
                "The vanilla fragment must be appended after the modded one, not replace it.");

            CollectionAssert.AreEqual(
                new[] { @"C:\packs\mod.pack", @"C:\packs\vanilla.pack" },
                FragmentPacks(resolved, "campaign_regions").ToArray(),
                "Each fragment must remember which pack it came from.");
        }

        [TestMethod]
        public void MatchTables_FolderPrefixMatchIsCaseInsensitive()
        {
            var resolved = NewResolvedDictionary();

            var matched = MatchTables(
                tables: new[] { "campaign_regions" },
                packEntries: new[] { "DB/Campaign_Regions_Tables/Fragment" },
                sourcePackPath: @"C:\packs\mod.pack",
                resolved: resolved);

            CollectionAssert.AreEqual(new[] { "campaign_regions" }, matched.ToArray(),
                "Pack entry casing must not decide whether a table is found.");
        }

        // "campaign_regions" must not swallow "campaign_regions_extra": the prefix includes the
        // "_tables/" separator precisely so a table name that extends another cannot collide.
        [TestMethod]
        public void MatchTables_TableNamesThatPrefixOneAnother_StaySeparate()
        {
            var resolved = NewResolvedDictionary();

            MatchTables(
                tables: new[] { "campaign_regions", "campaign_regions_extra" },
                packEntries: new[]
                {
                    "db/campaign_regions_tables/base",
                    "db/campaign_regions_extra_tables/extra",
                },
                sourcePackPath: @"C:\packs\mod.pack",
                resolved: resolved);

            CollectionAssert.AreEqual(new[] { "db/campaign_regions_tables/base" },
                FragmentEntries(resolved, "campaign_regions").ToArray(), "campaign_regions");
            CollectionAssert.AreEqual(new[] { "db/campaign_regions_extra_tables/extra" },
                FragmentEntries(resolved, "campaign_regions_extra").ToArray(), "campaign_regions_extra");
        }

        private static IDictionary NewResolvedDictionary()
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), TableSourceType);
            return (IDictionary)Activator.CreateInstance(dictType);
        }

        private static List<string> MatchTables(
            string[] tables, string[] packEntries, string sourcePackPath, IDictionary resolved)
        {
            var method = SessionType.GetMethod("MatchTables", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "RpfmWorkflowSession.MatchTables was not found.");

            return (List<string>)method.Invoke(null,
                new object[] { tables, packEntries, sourcePackPath, resolved });
        }

        // TableSource.Fragments is a List<(string PackPath, string Entry)> on a private nested type.
        private static IEnumerable<object> Fragments(IDictionary resolved, string table)
        {
            Assert.IsTrue(resolved.Contains(table), $"Table '{table}' was not resolved.");

            var source    = resolved[table];
            var fragments = (IEnumerable)TableSourceType.GetField("Fragments").GetValue(source);

            return fragments.Cast<object>();
        }

        private static IEnumerable<string> FragmentEntries(IDictionary resolved, string table)
            => Fragments(resolved, table).Select(f => (string)f.GetType().GetField("Item2").GetValue(f));

        private static IEnumerable<string> FragmentPacks(IDictionary resolved, string table)
            => Fragments(resolved, table).Select(f => (string)f.GetType().GetField("Item1").GetValue(f));
    }
}
