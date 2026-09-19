using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CAIME.Rpfm
{
    /// <summary>Outcome of a single rpfm_cli.exe invocation.</summary>
    public sealed class RpfmProcessResult
    {
        public bool     TimedOut    { get; set; }
        public int      ExitCode    { get; set; }
        public string   StdOut      { get; set; }
        public string   StdErr      { get; set; }

        public bool Succeeded => !TimedOut && ExitCode == 0;
    }

    /// <summary>
    /// Thin wrapper around <c>rpfm_cli.exe</c>. Owns process invocation only - every call captures
    /// stdout, stderr, the exit code and enforces a timeout. It contains no workflow logic; callers
    /// interpret the results. The RPFM installation folder is supplied once at construction.
    /// </summary>
    public sealed class RpfmService
    {
        private const int DefaultTimeoutMs = 120_000;

        /// <summary>Ceiling for a batched extraction, however many files it covers.</summary>
        private const int MaxBatchTimeoutMs = 15 * 60_000;

        // Strips the ANSI colour escape codes RPFM writes around its log lines so output can be parsed.
        private static readonly Regex AnsiEscape = new Regex(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

        private readonly string _rpfmFolder;

        public RpfmService(string rpfmFolder)
        {
            _rpfmFolder = rpfmFolder;
        }

        public string CliPath => Path.Combine(_rpfmFolder ?? string.Empty, "rpfm_cli.exe");

        /// <summary>
        /// Validates that <paramref name="rpfmFolder"/> is a usable RPFM installation by running
        /// <c>rpfm_cli.exe help</c> in it. Returns true only when the process succeeds and produces
        /// the expected RPFM output. On failure <paramref name="error"/> explains why.
        /// </summary>
        public static bool ValidateInstallation(string rpfmFolder, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(rpfmFolder) || !Directory.Exists(rpfmFolder))
            {
                error = "The selected folder does not exist.";
                return false;
            }

            var cliPath = Path.Combine(rpfmFolder, "rpfm_cli.exe");
            if (!File.Exists(cliPath))
            {
                error = "rpfm_cli.exe was not found in the selected folder.";
                return false;
            }

            var service = new RpfmService(rpfmFolder);
            var result  = service.Run("help", DefaultTimeoutMs);

            if (result.TimedOut)
            {
                error = "rpfm_cli.exe did not respond in time.";
                return false;
            }

            var combined = (result.StdOut ?? string.Empty) + (result.StdErr ?? string.Empty);
            if (result.ExitCode != 0 || combined.IndexOf("rpfm_cli", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = "The selected folder does not look like a valid RPFM installation " +
                        $"(rpfm_cli.exe help exited with code {result.ExitCode}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns every db table entry contained in the pack, each as a pack-relative path of the
        /// form <c>db/&lt;table_folder&gt;/&lt;file&gt;</c>. Throws on process failure.
        /// </summary>
        public IReadOnlyList<string> ListDbEntries(GameTemplate game, string packPath)
        {
            var gameId = GameMappingProvider.GetCliGameId(game);
            var args   = $"--game {gameId} pack list --pack-path {Quote(packPath)}";
            var result = Run(args, DefaultTimeoutMs);

            if (!result.Succeeded)
            {
                throw new RpfmException("list pack contents", result);
            }

            var entries = new List<string>();
            var text    = AnsiEscape.Replace(result.StdOut ?? string.Empty, string.Empty);

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                // db entries look like: db/<table>_tables/<file>. Ignore log lines and other file types.
                if (line.StartsWith("db/", StringComparison.OrdinalIgnoreCase) && line.IndexOf('/', 3) > 0)
                {
                    entries.Add(line);
                }
            }

            return entries;
        }

        /// <summary>
        /// Extracts a single db file from the pack into <paramref name="destinationFolder"/>,
        /// preserving the pack's internal <c>db/&lt;table&gt;/&lt;file&gt;</c> directory structure.
        /// Throws on process failure.
        /// </summary>
        public void ExtractDbFile(GameTemplate game, string packPath, string schemaPath, string inPackPath, string destinationFolder)
        {
            ExtractDbFiles(game, packPath, schemaPath, new[] { inPackPath }, destinationFolder);
        }

        /// <summary>
        /// Extracts several db files from one pack in a single rpfm_cli invocation. -f is repeatable,
        /// so a pack contributing fragments of twenty tables costs one process rather than twenty.
        /// </summary>
        public void ExtractDbFiles(GameTemplate game, string packPath, string schemaPath, IReadOnlyList<string> inPackPaths, string destinationFolder)
        {
            if (inPackPaths.Count == 0)
            {
                return;
            }

            var gameId = GameMappingProvider.GetCliGameId(game);

            // -f takes "<file_in_pack>;<folder_to_extract_to>" and may be repeated.
            var args = new StringBuilder();
            args.Append($"-g {gameId} pack extract --pack-path {Quote(packPath)} -t {Quote(schemaPath)}");

            foreach (var inPackPath in inPackPaths)
            {
                args.Append(" -f ").Append(Quote($"{inPackPath};{destinationFolder}"));
            }

            // The budget scales with the batch so one call is not held to a single file's allowance.
            var timeoutMs = Math.Min((long)DefaultTimeoutMs * inPackPaths.Count, MaxBatchTimeoutMs);

            var result = Run(args.ToString(), (int)timeoutMs);
            if (!result.Succeeded)
            {
                throw new RpfmException($"extract {inPackPaths.Count} file(s) from '{Path.GetFileName(packPath)}'", result);
            }
        }

        private RpfmProcessResult Run(string arguments, int timeoutMs)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName               = CliPath,
                Arguments              = arguments,
                WorkingDirectory       = _rpfmFolder,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using (var process = new Process { StartInfo = startInfo })
            {
                // Async reads on both streams avoid the classic full-pipe deadlock.
                process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived  += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { /* best effort */ }
                    return new RpfmProcessResult
                    {
                        TimedOut = true,
                        ExitCode = -1,
                        StdOut   = stdout.ToString(),
                        StdErr   = stderr.ToString(),
                    };
                }

                // Ensure the async buffers are flushed before we read them.
                process.WaitForExit();

                return new RpfmProcessResult
                {
                    TimedOut = false,
                    ExitCode = process.ExitCode,
                    StdOut   = stdout.ToString(),
                    StdErr   = stderr.ToString(),
                };
            }
        }

        private static string Quote(string value) => "\"" + value + "\"";
    }

    /// <summary>Raised when an rpfm_cli.exe invocation fails; carries the captured output for logging.</summary>
    public sealed class RpfmException : Exception
    {
        public RpfmProcessResult Result { get; }

        public RpfmException(string operation, RpfmProcessResult result)
            : base(BuildMessage(operation, result))
        {
            Result = result;
        }

        private static string BuildMessage(string operation, RpfmProcessResult result)
        {
            if (result.TimedOut)
            {
                return $"RPFM failed to {operation}: the operation timed out.";
            }

            var detail = result.StdErr;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = result.StdOut;
            }

            return $"RPFM failed to {operation} (exit code {result.ExitCode}). {detail?.Trim()}";
        }
    }
}
