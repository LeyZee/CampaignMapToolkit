using System;
using System.Collections.Generic;
using System.IO;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Centralised mapping between CAIME's <see cref="GameTemplate"/> and the identifiers RPFM
    /// expects. Keeps the RPFM-specific knowledge (CLI <c>-g</c> value and per-game schema file
    /// name) in a single place so the rest of the codebase never switches on game type for RPFM.
    ///
    /// Only games RPFM supports for this workflow are listed here; anything absent is treated as
    /// "not supported" (see <see cref="IsSupported"/>).
    /// </summary>
    public static class GameMappingProvider
    {
        private sealed class RpfmGameInfo
        {
            public string CliId;        // value passed to rpfm_cli.exe -g / --game
            public string SchemaFile;   // schema_*.ron file name for this game
        }

        // GameTemplate -> RPFM identifiers. Games not present are unsupported by the RPFM workflow.
        private static readonly Dictionary<GameTemplate, RpfmGameInfo> Map = new Dictionary<GameTemplate, RpfmGameInfo>
        {
            [GameTemplate.Rome2]                = new RpfmGameInfo { CliId = "rome_2",               SchemaFile = "schema_rom2.ron" },
            [GameTemplate.Attila]               = new RpfmGameInfo { CliId = "attila",               SchemaFile = "schema_att.ron" },
            [GameTemplate.Thrones_Of_Britannia] = new RpfmGameInfo { CliId = "thrones_of_britannia", SchemaFile = "schema_tob.ron" },
            [GameTemplate.Warhammer]            = new RpfmGameInfo { CliId = "warhammer",            SchemaFile = "schema_wh.ron" },
            [GameTemplate.Warhammer2]           = new RpfmGameInfo { CliId = "warhammer_2",          SchemaFile = "schema_wh2.ron" },
            [GameTemplate.Warhammer3]           = new RpfmGameInfo { CliId = "warhammer_3",          SchemaFile = "schema_wh3.ron" },
            [GameTemplate.Three_Kingdoms]       = new RpfmGameInfo { CliId = "three_kingdoms",       SchemaFile = "schema_3k.ron" },
            [GameTemplate.Troy]                 = new RpfmGameInfo { CliId = "troy",                 SchemaFile = "schema_troy.ron" },
            [GameTemplate.Pharaoh]              = new RpfmGameInfo { CliId = "pharaoh",              SchemaFile = "schema_ph.ron" },
            [GameTemplate.Pharaoh_Dynasties]    = new RpfmGameInfo { CliId = "pharaoh_dynasties",    SchemaFile = "schema_ph_dyn.ron" },
        };

        /// <summary>True when the RPFM workflow supports the given game.</summary>
        public static bool IsSupported(GameTemplate game) => Map.ContainsKey(game);

        /// <summary>
        /// The value to pass to rpfm_cli.exe via <c>-g</c>/<c>--game</c>.
        /// Throws <see cref="NotSupportedException"/> for unsupported games.
        /// </summary>
        public static string GetCliGameId(GameTemplate game)
        {
            if (Map.TryGetValue(game, out var info))
            {
                return info.CliId;
            }

            throw new NotSupportedException($"The RPFM workflow does not support {game}.");
        }

        /// <summary>The schema_*.ron file name RPFM uses for this game.</summary>
        public static string GetSchemaFileName(GameTemplate game)
        {
            if (Map.TryGetValue(game, out var info))
            {
                return info.SchemaFile;
            }

            throw new NotSupportedException($"The RPFM workflow does not support {game}.");
        }

        /// <summary>
        /// Absolute path to the game's RPFM schema file. RPFM stores its schemas under
        /// <c>%AppData%\FrodoWazEre\rpfm\config\schemas</c>.
        /// </summary>
        public static string GetSchemaPath(GameTemplate game)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "FrodoWazEre", "rpfm", "config", "schemas", GetSchemaFileName(game));
        }
    }
}
