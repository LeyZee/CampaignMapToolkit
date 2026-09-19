using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Owns one run of the RPFM database preparation pipeline as a single transaction. For each
    /// required table it gathers every fragment of that table found anywhere - the vanilla pack and
    /// every one of the project's modded packs - and merges them the same way the game itself does:
    /// when two fragments define a row for the same primary key, the fragment whose name sorts
    /// earlier wins; rows unique to any one fragment are all kept. In the rare case where two packs
    /// share the exact same fragment name too (so fragment name breaks no tie), order of consultation
    /// decides it: the modded packs are read first, in pack-file-name order (see the packPaths
    /// constructor parameter below), and the vanilla pack last - so a mod always beats vanilla on an
    /// identical fragment name whatever either file is called. It then backs up the original
    /// Assembly Kit db files, extracts and merges the gathered tables, converts them to Assembly Kit
    /// XML, and - crucially - guarantees that the Assembly Kit is left exactly as it was found once
    /// the session is cleaned up (whether the project closes, another opens, the app exits, or any
    /// step fails).
    ///
    /// Only the vanilla pack is required (see <see cref="ValidatePreconditions"/>) - it's the actual
    /// data source RPFM exists to read from. The modded packs are optional: a project with none
    /// configured simply reads every table from vanilla, and a configured-but-unreadable modded pack
    /// (deleted, moved, corrupted) degrades the same way rather than blocking the session - it's a
    /// per-project convenience layered on top, not something that should be able to prevent a project
    /// from opening.
    ///
    /// The vanilla pack path is supplied by the caller rather than guessed: which pack actually
    /// contains a game's DB tables is not a stable filename - it has changed between games (e.g.
    /// Warhammer 2's data is split across several data*.pack files) and even within one game's
    /// lifetime (Warhammer 3 moved its DB tables out of data.pack and into db.pack after release).
    /// There is also no way to distinguish CA's own packs from mods sitting in the same data folder
    /// via the RPFM CLI, so this cannot be auto-detected reliably.
    ///
    /// Usage:
    ///   var session = new RpfmWorkflowSession(...);
    ///   session.Prepare();          // throws on failure, having already rolled everything back
    ///   ... existing DB loading ...
    ///   session.Cleanup();          // restores originals, removes generated files
    /// </summary>
    public sealed class RpfmWorkflowSession : IDisposable
    {
        // Best-effort crash recovery: any session that has begun mutating the Assembly Kit registers
        // here so a process exit / unhandled exception can still restore the originals.
        private static readonly HashSet<RpfmWorkflowSession> ActiveSessions = new HashSet<RpfmWorkflowSession>();
        private static readonly object ActiveSessionsLock = new object();

        static RpfmWorkflowSession()
        {
            AppDomain.CurrentDomain.ProcessExit       += (s, e) => CleanupAll();
            AppDomain.CurrentDomain.UnhandledException += (s, e) => CleanupAll();
        }

        // Every fragment of a resolved table found across all sources (vanilla and every modded
        // pack), each tagged with the pack it came from so it can be extracted from the right place.
        private sealed class TableSource
        {
            public readonly List<(string PackPath, string Entry)> Fragments = new List<(string, string)>();
        }

        private readonly GameTemplate           _game;
        private readonly string                 _dbRootPath;
        private readonly IReadOnlyList<string>  _packPaths;
        private readonly string                 _vanillaPackPath;
        private readonly string                 _schemaPath;
        private readonly IReadOnlyDictionary<string, bool> _regionIsSeaByKey;
        private readonly RpfmService             _rpfm;
        private readonly BackupService           _backup;
        private readonly string                 _tempExtractDir;
        private readonly List<string>            _generatedFiles = new List<string>();
        private readonly RpfmRecoveryJournal     _journal;

        private bool _cleanedUp;

        /// <param name="packPaths">
        /// The project's modded packs. Optional - pass null or empty when the project has none
        /// configured; every table then comes from the vanilla pack instead. Every configured pack is
        /// consulted for every required table, and any fragments found are merged with the vanilla
        /// pack's (see <see cref="ResolvePresentTables"/>). Sorted here by pack file name - not a
        /// user-chosen priority, just the tiebreak used when two of these modded packs contain a
        /// fragment with the exact same name (see the class summary above).
        /// </param>
        /// <param name="vanillaPackPath">
        /// The user-selected pack containing this game's vanilla DB tables. Required - see
        /// <see cref="ValidatePreconditions"/>.
        /// </param>
        /// <param name="mapHex">
        /// The project's already-loaded map, used only as a ground-truth source for "regions.is_sea"
        /// (see <see cref="BuildRegionSeaStatus"/>). Optional - pass null when there is no map; that
        /// field then falls back to the existing Assembly Kit record or the schema default same as any
        /// other field RPFM cannot supply.
        /// </param>
        public RpfmWorkflowSession(GameTemplate game, string assemblyKitPath, string rpfmFolder, IReadOnlyList<string> packPaths, string vanillaPackPath, MapHexFile mapHex = null)
        {
            _game            = game;
            _dbRootPath      = Path.Combine(assemblyKitPath, "raw_data", "db");
            _packPaths       = (packPaths ?? Array.Empty<string>())
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();
            _vanillaPackPath = vanillaPackPath;
            _schemaPath      = GameMappingProvider.GetSchemaPath(game);
            _regionIsSeaByKey = BuildRegionSeaStatus(mapHex);
            _rpfm            = new RpfmService(rpfmFolder);

            // Both working directories are private to this session and sit outside the db root.
            // The old one-second-resolution timestamps inside the db root meant two sessions
            // started in the same second shared them: the second session's File.Move into an
            // occupied path threw, and either session's cleanup destroyed the other's originals.
            var sessionId    = Guid.NewGuid().ToString("N");
            var sessionRoot  = Path.Combine(assemblyKitPath, "caime_rpfm", sessionId);

            _backup          = new BackupService(_dbRootPath, Path.Combine(sessionRoot, "backup"));
            _tempExtractDir  = Path.Combine(sessionRoot, "extract");

            // Persisted record used to recover if the process is killed before cleanup can run.
            _journal = new RpfmRecoveryJournal
            {
                SessionId      = sessionId,
                DbRootPath     = _dbRootPath,
                BackupRootPath = _backup.BackupRootPath,
                TempExtractDir = _tempExtractDir,
            };
        }

        /// <summary>
        /// Runs the full preparation pipeline. On success the Assembly Kit db folder contains the
        /// resolved tables - each one merged from every pack (vanilla and modded) that contains a
        /// fragment of it - as Assembly Kit XML, ready for the normal loader. On any failure the
        /// Assembly Kit is fully restored and a descriptive exception is thrown.
        /// </summary>
        public void Prepare()
        {
            ValidatePreconditions();

            var requiredTables = DatabaseTableProvider.GetRequiredTables(_game);

            // Step 3 (inspect pack contents) is read-only, so run it before touching anything.
            // Table presence is a soft requirement: only the required tables found in the modded pack
            // or the vanilla pack take part in the workflow. Tables absent from both are left as-is,
            // so the normal loader falls back to the Assembly Kit's own copy. Finding nothing in
            // either pack is valid too - the workflow simply does nothing.
            var tableEntries = ResolvePresentTables(requiredTables);
            var presentTables = tableEntries.Keys.ToList();

            // From here on the Assembly Kit is mutated; any failure must roll everything back.
            try
            {
                // Write the recovery journal before touching any file, so a hard kill at any point
                // from here on leaves a record the next launch can replay.
                _journal.Save();

                BackupOriginals(presentTables);
                EnsureNoConflicts(presentTables);
                ExtractAndConvert(presentTables, tableEntries);

                // Step 5: temporary TSVs are no longer needed once converted.
                DeleteTempExtractDir();

                Register(this);
            }
            catch
            {
                RestoreAndCleanup();
                throw;
            }
        }

        /// <summary>
        /// Restores the original Assembly Kit files, removes generated XML and any temporary files,
        /// and deletes the backup directory. Idempotent.
        /// </summary>
        public void Cleanup()
        {
            RestoreAndCleanup();
        }

        public void Dispose()
        {
            RestoreAndCleanup();
        }

        // -----------------------------------------------------------------
        // Pipeline steps
        // -----------------------------------------------------------------

        private void ValidatePreconditions()
        {
            if (!GameMappingProvider.IsSupported(_game))
            {
                throw new NotSupportedException($"The RPFM workflow does not support {_game}.");
            }

            if (!Directory.Exists(_dbRootPath))
            {
                throw new DirectoryNotFoundException($"Assembly Kit db folder not found: {_dbRootPath}");
            }

            // The modded pack is intentionally not validated here - it's optional, and a
            // missing/stale one degrades gracefully in ResolvePresentTables rather than blocking.

            if (string.IsNullOrEmpty(_vanillaPackPath) || !File.Exists(_vanillaPackPath))
            {
                throw new FileNotFoundException($"Vanilla pack file not found: {_vanillaPackPath}");
            }

            if (!File.Exists(_schemaPath))
            {
                throw new FileNotFoundException(
                    $"RPFM schema for {_game} not found at {_schemaPath}. Open the game in RPFM once to generate its schema.");
            }
        }

        // Step 3: find every fragment of each required table, in every pack - every modded pack plus
        // the vanilla pack - rather than picking a single winning pack per table. Combining happens
        // later, per row, when the fragments are merged into XML (see ExtractAndConvert and
        // DatabaseTableConverter.MergeTsvToXml): a table added purely by a mod, a vanilla table
        // extended by a mod, and a table left untouched all fall out of the same merge naturally. The
        // vanilla pack (guaranteed present by ValidatePreconditions) is always consulted, so a failure
        // reading it propagates and aborts the workflow. A table present nowhere is simply left
        // unresolved (a soft requirement at the table level) - the normal loader then falls back to
        // the Assembly Kit's own copy for just that table.
        private Dictionary<string, TableSource> ResolvePresentTables(IReadOnlyList<string> requiredTables)
        {
            var resolved = new Dictionary<string, TableSource>();

            // The modded packs are optional and per-project - none configured at all is a normal
            // state, and a configured-but-broken one (deleted, moved, corrupted) shouldn't be able to
            // block the session either, so both cases just skip that pack rather than throwing.
            var fromModPacks = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var packPath in _packPaths)
            {
                if (string.IsNullOrEmpty(packPath))
                {
                    continue;
                }

                if (!File.Exists(packPath))
                {
                    LoggerViewModel.Log(
                        $"RPFM workflow: a modded pack recorded for this project no longer exists ({packPath}) - " +
                        "skipping it. Update the list via Settings > RPFM Workflow if needed.", LogLevel.Warning);
                    continue;
                }

                try
                {
                    var modEntries = _rpfm.ListDbEntries(_game, packPath);
                    foreach (var table in MatchTables(requiredTables, modEntries, packPath, resolved))
                    {
                        fromModPacks.Add(table);
                    }
                }
                catch (Exception ex)
                {
                    LoggerViewModel.Log(
                        $"RPFM workflow: could not read a modded pack ({packPath}) - {ex.Message}. " +
                        "Skipping it.", LogLevel.Warning);
                }
            }

            var vanillaEntries = _rpfm.ListDbEntries(_game, _vanillaPackPath);
            var fromVanilla = MatchTables(requiredTables, vanillaEntries, _vanillaPackPath, resolved);

            if (resolved.Count == 0)
            {
                LoggerViewModel.Log(
                    "RPFM workflow: none of the required database tables were found in the modded packs or the " +
                    "vanilla pack. The Assembly Kit's own tables will be used unchanged.", LogLevel.Info);
                return resolved;
            }

            if (fromModPacks.Count > 0)
            {
                LoggerViewModel.Log("RPFM workflow: merging tables found in the modded packs: " + string.Join(", ", fromModPacks), LogLevel.Info);
            }

            if (fromVanilla.Count > 0)
            {
                LoggerViewModel.Log("RPFM workflow: merging tables found in the vanilla pack: " + string.Join(", ", fromVanilla), LogLevel.Info);
            }

            var stillMissing = requiredTables.Where(t => !resolved.ContainsKey(t)).ToList();
            if (stillMissing.Count > 0)
            {
                LoggerViewModel.Log(
                    "RPFM workflow: these tables were not found in the modded packs or the vanilla pack and " +
                    "will be loaded from the Assembly Kit: " + string.Join(", ", stillMissing), LogLevel.Info);
            }

            return resolved;
        }

        // Matches whichever of `tables` are present in `packEntries` and appends their fragments to
        // `resolved`, adding to any fragments already recorded for that table from another pack rather
        // than replacing them. Returns the tables just matched in this pack.
        private static List<string> MatchTables(
            IReadOnlyList<string> tables, IReadOnlyList<string> packEntries, string sourcePackPath,
            Dictionary<string, TableSource> resolved)
        {
            var matched = new List<string>();

            foreach (var table in tables)
            {
                var folderPrefix = $"db/{table}_tables/";
                var entries = packEntries
                    .Where(e => e.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (entries.Count == 0)
                {
                    continue;
                }

                if (!resolved.TryGetValue(table, out var source))
                {
                    source = new TableSource();
                    resolved[table] = source;
                }

                foreach (var entry in entries)
                {
                    source.Fragments.Add((sourcePackPath, entry));
                }

                matched.Add(table);
            }

            return matched;
        }

        // Step 2: move the original data XML for each table being replaced into the backup directory.
        // Only tables sourced from the pack are backed up; everything else stays untouched.
        private void BackupOriginals(IReadOnlyList<string> tablesToReplace)
        {
            foreach (var table in tablesToReplace)
            {
                _backup.Backup(Path.Combine(_dbRootPath, table + ".xml"));
            }
        }

        // Step 4 (pre-extraction): abort if a conflicting file for any table already exists,
        // regardless of extension. After the backup moved the originals aside, the only way a match
        // survives is a stray leftover from an earlier interrupted run.
        private void EnsureNoConflicts(IReadOnlyList<string> tables)
        {
            foreach (var table in tables)
            {
                var conflicts = Directory.GetFiles(_dbRootPath, table + ".*", SearchOption.TopDirectoryOnly);
                if (conflicts.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"A conflicting file already exists in the Assembly Kit db folder: {conflicts[0]}");
                }
            }
        }

        // Steps 4 & 5: extract each present table's fragments from every pack that contributed one and
        // merge them into Assembly Kit XML.
        private void ExtractAndConvert(IReadOnlyList<string> tables, Dictionary<string, TableSource> tableSources)
        {
            if (tables.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(_tempExtractDir);

            // Fragments from different packs can share the exact same in-pack path (e.g. every pack's
            // default "db/<table>_tables/data"), so each contributing pack gets its own extraction
            // subfolder to avoid one pack's fragment overwriting another's on disk.
            var packSubDirs = new Dictionary<string, string>();
            int packIndex = 0;

            // Every fragment this run needs, grouped by the pack it comes from, so each pack is
            // opened once instead of once per table fragment.
            var entriesByPack = new Dictionary<string, List<string>>();

            foreach (var table in tables)
            {
                foreach (var (packPath, entry) in tableSources[table].Fragments)
                {
                    if (!packSubDirs.ContainsKey(packPath))
                    {
                        packSubDirs[packPath] = Path.Combine(_tempExtractDir, "pack" + packIndex++);
                    }

                    if (!entriesByPack.TryGetValue(packPath, out var entries))
                    {
                        entries = new List<string>();
                        entriesByPack[packPath] = entries;
                    }

                    entries.Add(entry);
                }
            }

            foreach (var pack in entriesByPack)
            {
                _rpfm.ExtractDbFiles(_game, pack.Key, _schemaPath, pack.Value, packSubDirs[pack.Key]);
            }

            foreach (var table in tables)
            {
                var source = tableSources[table];

                var twadSchemaPath = Path.Combine(_dbRootPath, "TWaD_" + table + ".xml");
                if (!File.Exists(twadSchemaPath))
                {
                    throw new FileNotFoundException(
                        $"Assembly Kit schema file missing: {twadSchemaPath}. The Assembly Kit installation looks incomplete.");
                }

                // One parse of the schema file, not three.
                var schema            = DatabaseTableConverter.LoadSchema(twadSchemaPath);
                var booleanColumns    = DatabaseTableConverter.GetBooleanColumns(schema);
                var primaryKeyColumns = DatabaseTableConverter.GetPrimaryKeyColumns(schema);
                var schemaFields      = schema.Fields;

                // BackupOriginals already moved this table's pre-existing Assembly Kit XML here (if it
                // had one) before this method runs - read it back as a fallback source for fields RPFM
                // has no way to supply (see MergeTsvToXml).
                var backedUpXmlPath = Path.Combine(_backup.BackupRootPath, table + ".xml");
                var existingRecords = DatabaseTableConverter.LoadExistingRecords(backedUpXmlPath, table, primaryKeyColumns);

                var fragments = new List<(string TsvPath, string FragmentName)>();
                foreach (var (packPath, entry) in source.Fragments)
                {
                    if (!packSubDirs.TryGetValue(packPath, out var subDir))
                    {
                        subDir = Path.Combine(_tempExtractDir, "pack" + packIndex++);
                        packSubDirs[packPath] = subDir;
                    }

                    var tsvPath = Path.Combine(subDir, entry.Replace('/', Path.DirectorySeparatorChar)) + ".tsv";
                    if (!File.Exists(tsvPath))
                    {
                        throw new FileNotFoundException($"RPFM extraction produced no file for '{entry}'.");
                    }

                    // The fragment's own filename (the part after the table folder) decides merge
                    // order when two sources define the same primary key - the same rule the game uses
                    // to combine table fragments across packs.
                    var fragmentName = entry.Substring(entry.LastIndexOf('/') + 1);
                    fragments.Add((tsvPath, fragmentName));
                }

                var outputXml = Path.Combine(_dbRootPath, table + ".xml");
                DatabaseTableConverter.MergeTsvToXml(fragments, table, booleanColumns, primaryKeyColumns, schemaFields, existingRecords, _regionIsSeaByKey, outputXml);
                _generatedFiles.Add(outputXml);

                // Record the generated file only now that it exists and its original is safely backed
                // up, so crash-recovery can delete it without risking an untouched original.
                _journal.GeneratedFiles.Add(outputXml);
                _journal.Save();
            }
        }

        // Ground truth for "regions.is_sea": which regions this map treats as sea. That field never
        // appears in any pack's raw table data at any version - the Assembly Kit computes it itself -
        // and the Assembly Kit's own regions.xml can be arbitrarily stale (it will not know a region
        // added since the last export at all), so the open map is the only source that is right by
        // construction. It is already parsed and in memory by the time a session is built: Project.Open
        // loads the map first and only then runs the hook that prepares this workflow.
        private static IReadOnlyDictionary<string, bool> BuildRegionSeaStatus(MapHexFile mapHex)
        {
            // Absent regions are "unknown to this map" rather than "land", so callers fall back to
            // another source for them instead of being told something wrong.
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var region in mapHex?.LandRegions ?? Enumerable.Empty<string>())
            {
                result[region] = false;
            }

            foreach (var region in mapHex?.SeaRegions ?? Enumerable.Empty<string>())
            {
                result[region] = true;
            }

            return result;
        }

        // -----------------------------------------------------------------
        // Teardown / rollback (shared by success cleanup and failure rollback)
        // -----------------------------------------------------------------

        private void RestoreAndCleanup()
        {
            if (_cleanedUp)
            {
                return;
            }

            _cleanedUp = true;
            Unregister(this);

            // Remove generated XML first, then move the originals back over the top.
            foreach (var generated in _generatedFiles)
            {
                TryDeleteFile(generated);
            }
            _generatedFiles.Clear();

            DeleteTempExtractDir();

            try
            {
                _backup.Restore();
                _backup.DeleteBackupDirectory();
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM cleanup - failed to restore Assembly Kit backup: {ex.Message}", LogLevel.Error);
            }

            // The Assembly Kit is restored - the recovery journal is no longer needed. Deleted last so
            // that a crash at any earlier point still leaves it for the next launch to replay.
            _journal.Delete();
        }

        private void DeleteTempExtractDir()
        {
            try
            {
                if (Directory.Exists(_tempExtractDir))
                {
                    Directory.Delete(_tempExtractDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM cleanup - failed to delete temporary extraction folder: {ex.Message}", LogLevel.Warning);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"RPFM cleanup - failed to delete generated file {path}: {ex.Message}", LogLevel.Warning);
            }
        }

        // -----------------------------------------------------------------
        // Active-session registry for crash recovery
        // -----------------------------------------------------------------

        private static void Register(RpfmWorkflowSession session)
        {
            lock (ActiveSessionsLock)
            {
                ActiveSessions.Add(session);
            }
        }

        private static void Unregister(RpfmWorkflowSession session)
        {
            lock (ActiveSessionsLock)
            {
                ActiveSessions.Remove(session);
            }
        }

        private static void CleanupAll()
        {
            RpfmWorkflowSession[] snapshot;
            lock (ActiveSessionsLock)
            {
                snapshot = ActiveSessions.ToArray();
            }

            foreach (var session in snapshot)
            {
                try { session.RestoreAndCleanup(); } catch { /* best effort during shutdown */ }
            }
        }
    }
}
