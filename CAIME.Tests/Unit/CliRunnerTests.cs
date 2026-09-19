using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="CliRunner"/>'s argument grammar. The CLI is the headless entry
    /// point used for batch and scripted exports, where a misparsed invocation either does nothing
    /// or processes the wrong map with no one watching.
    ///
    /// <para>
    /// These drive the private <c>ParseInvocation</c> and <c>Dispatch</c> directly, stopping short of
    /// the work each would go on to do - opening a project needs a configured Assembly Kit.
    /// </para>
    /// </summary>
    [TestClass]
    public class CliRunnerTests
    {
        private static readonly Type CliType = typeof(CliRunner);

        private string _dir;
        private string _mapPath;
        private TextWriter _stdout;
        private TextWriter _stderr;
        private StringWriter _captured;

        [TestInitialize]
        public void Setup()
        {
            _dir     = MapHexHarness.CreateTempDir("caime_cli");
            _mapPath = Path.Combine(_dir, "map.hex");
            File.WriteAllText(_mapPath, "not a real map - parsing never opens it");

            _captured = new StringWriter();
            _stdout   = Console.Out;
            _stderr   = Console.Error;
            Console.SetOut(_captured);
            Console.SetError(_captured);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Console.SetOut(_stdout);
            Console.SetError(_stderr);
            _captured?.Dispose();
            MapHexHarness.DeleteTempDir(_dir);
        }

        [TestMethod]
        public void Parse_SingleTask_SelectsOnlyThatTask()
        {
            var result = ParseProcess("--map", _mapPath, "--pathfinding");

            Assert.IsNull(result.ExitCode, "A valid invocation must parse.");
            CollectionAssert.AreEqual(new[] { "Pathfinding" }, result.Tasks.ToArray(), "Selected tasks");
            Assert.AreEqual(Path.GetFullPath(_mapPath), result.MapPath, "Map path");
        }

        // Tasks always run in the canonical order declared on the Tasks table, never the order they
        // were typed - later stages read files earlier ones produce.
        [TestMethod]
        public void Parse_SeveralTasks_RunInCanonicalOrderNotArgumentOrder()
        {
            var result = ParseProcess("--map", _mapPath, "--trade-routes", "--map-data", "--pathfinding");

            Assert.IsNull(result.ExitCode, "A valid invocation must parse.");
            CollectionAssert.AreEqual(
                new[] { "MapData", "Pathfinding", "TradeRoutes" },
                result.Tasks.ToArray(),
                "Tasks must be reordered into the canonical run order.");
        }

        [TestMethod]
        public void Parse_All_SelectsEveryTaskInCanonicalOrder()
        {
            var result = ParseProcess("--map", _mapPath, "--all");

            Assert.IsNull(result.ExitCode, "A valid invocation must parse.");
            CollectionAssert.AreEqual(
                new[] { "MapData", "DynamicResources", "Pathfinding", "Borders", "TradeRoutes", "Lookup" },
                result.Tasks.ToArray(),
                "--all must expand to every task in canonical order.");
        }

        [TestMethod]
        public void Parse_RepeatedTask_IsRequestedOnce()
        {
            var result = ParseProcess("--map", _mapPath, "--pathfinding", "--pathfinding");

            Assert.IsNull(result.ExitCode, "A valid invocation must parse.");
            CollectionAssert.AreEqual(new[] { "Pathfinding" }, result.Tasks.ToArray(),
                "A task named twice must not run twice.");
        }

        [TestMethod]
        public void Parse_ShortMapFlagAndFlagCasing_AreAccepted()
        {
            var result = ParseProcess("-m", _mapPath, "--PATHFINDING");

            Assert.IsNull(result.ExitCode, "Flag casing must not decide whether an invocation parses.");
            CollectionAssert.AreEqual(new[] { "Pathfinding" }, result.Tasks.ToArray(), "Selected tasks");
        }

        [TestMethod]
        public void Parse_RelativeMapPath_IsResolvedToAFullPath()
        {
            var previous = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_dir);

                var result = ParseProcess("--map", "map.hex", "--pathfinding");

                Assert.IsNull(result.ExitCode, "A relative path must parse.");
                Assert.AreEqual(Path.GetFullPath(_mapPath), result.MapPath,
                    "The map path must be resolved before anything downstream uses it.");
            }
            finally
            {
                Directory.SetCurrentDirectory(previous);
            }
        }

        [TestMethod]
        public void Parse_MissingMapOption_IsRejected()
        {
            var result = ParseProcess("--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "A run with no map must fail with the usage exit code.");
            StringAssert.Contains(_captured.ToString(), "--map", "The error should name the missing option.");
        }

        // "--map --pathfinding" would otherwise consume the next flag as a file name.
        [TestMethod]
        public void Parse_MapOptionFollowedByAnotherFlag_IsRejected()
        {
            var result = ParseProcess("--map", "--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "--map with no value must be rejected.");
        }

        [TestMethod]
        public void Parse_MapOptionAsTheLastArgument_IsRejected()
        {
            var result = ParseProcess("--pathfinding", "--map");

            Assert.AreEqual(1, result.ExitCode, "A trailing --map with no value must be rejected.");
        }

        [TestMethod]
        public void Parse_NoTasks_IsRejected()
        {
            var result = ParseProcess("--map", _mapPath);

            Assert.AreEqual(1, result.ExitCode, "An invocation that would do nothing must be rejected.");
        }

        // --all plus a task is ambiguous: the user either wants everything or one thing.
        [TestMethod]
        public void Parse_AllCombinedWithATask_IsRejected()
        {
            var result = ParseProcess("--map", _mapPath, "--all", "--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "--all combined with an individual task must be rejected.");
        }

        [TestMethod]
        public void Parse_UnknownOption_IsRejected()
        {
            var result = ParseProcess("--map", _mapPath, "--not-a-task");

            Assert.AreEqual(1, result.ExitCode, "An unknown option must be rejected.");
            StringAssert.Contains(_captured.ToString(), "--not-a-task", "The error should name the offending option.");
        }

        [TestMethod]
        public void Parse_NonHexExtension_IsRejected()
        {
            var wrongExtension = Path.Combine(_dir, "map.esf");
            File.WriteAllText(wrongExtension, "wrong file type");

            var result = ParseProcess("--map", wrongExtension, "--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "Only a .hex file may be processed.");
        }

        [TestMethod]
        public void Parse_MissingMapFile_IsRejected()
        {
            var result = ParseProcess("--map", Path.Combine(_dir, "absent.hex"), "--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "A map file that does not exist must be rejected.");
        }

        [TestMethod]
        public void Parse_HelpFlagAnywhere_PrintsHelpAndSucceeds()
        {
            var result = ParseProcess("--map", _mapPath, "--help");

            Assert.AreEqual(0, result.ExitCode, "--help must exit successfully.");
            StringAssert.Contains(_captured.ToString(), "--pathfinding", "Help should list the available tasks.");
        }

        [TestMethod]
        public void Parse_ValidateCommand_UsesItsOwnTaskTable()
        {
            var result = ParseValidate("--map", _mapPath, "--bridges", "--rivers");

            Assert.IsNull(result.ExitCode, "A valid validate invocation must parse.");
            CollectionAssert.AreEqual(new[] { "Rivers", "Bridges" }, result.Tasks.ToArray(),
                "Validations must run in the order the Validate menu declares.");
        }

        // The process and validate grammars share a parser but not their flags.
        [TestMethod]
        public void Parse_ProcessFlagPassedToValidate_IsRejected()
        {
            var result = ParseValidate("--map", _mapPath, "--pathfinding");

            Assert.AreEqual(1, result.ExitCode, "A process-only flag must not be accepted by validate.");
        }

        [TestMethod]
        public void Dispatch_UnknownCommand_IsRejected()
        {
            var exitCode = Dispatch("frobnicate");

            Assert.AreEqual(1, exitCode, "An unknown command must fail with the usage exit code.");
            StringAssert.Contains(_captured.ToString(), "frobnicate", "The error should name the unknown command.");
        }

        [TestMethod]
        public void Dispatch_HelpCommandAndFlags_PrintHelpAndSucceed()
        {
            foreach (var form in new[] { "help", "--help", "-h" })
            {
                Assert.AreEqual(0, Dispatch(form), $"'{form}' must exit successfully.");
            }

            StringAssert.Contains(_captured.ToString(), "process", "Help should list the available commands.");
        }

        private sealed class ParseResult
        {
            public int? ExitCode;
            public string MapPath;
            public List<string> Tasks = new List<string>();
        }

        private static ParseResult ParseProcess(params string[] args) => Parse("Tasks", "ProcessTask", args);

        private static ParseResult ParseValidate(params string[] args) => Parse("ValidateTasks", "ValidateTask", args);

        private static ParseResult Parse(string tableFieldName, string taskEnumName, string[] args)
        {
            var taskEnumType = CliType.GetNestedType(taskEnumName, BindingFlags.NonPublic);
            Assert.IsNotNull(taskEnumType, $"CliRunner.{taskEnumName} was not found.");

            var table = CliType.GetField(tableFieldName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            Assert.IsNotNull(table, $"CliRunner.{tableFieldName} was not found.");

            var method = CliType.GetMethod("ParseInvocation", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "CliRunner.ParseInvocation was not found.");

            var parameters = new object[] { args, table, null, null };
            var exitCode   = method.MakeGenericMethod(taskEnumType).Invoke(null, parameters);

            var result = new ParseResult
            {
                ExitCode = (int?)exitCode,
                MapPath  = (string)parameters[2],
            };

            if (parameters[3] is IEnumerable tasks)
            {
                result.Tasks.AddRange(tasks.Cast<object>().Select(t => t.ToString()));
            }

            return result;
        }

        private static int Dispatch(params string[] args)
        {
            var method = CliType.GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "CliRunner.Dispatch was not found.");

            return (int)method.Invoke(null, new object[] { args });
        }
    }
}
