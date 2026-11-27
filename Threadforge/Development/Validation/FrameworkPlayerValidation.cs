[assembly: UnityEditor.TestTools.TestPlayerBuildModifier(typeof(Threadlink.Framework.Editor.Tests.FrameworkPlayerValidation.HeadlessPlayerBuild))]

namespace Threadlink.Framework.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.TestTools;
    using UnityEditor.TestTools.TestRunner.Api;
    using UnityEngine;

    /// <summary>
    /// Builds the IL2CPP test player and runs it headless (batch mode, no window), reading the results the player writes
    /// with <c>PlayerResultWriter</c>. Nothing takes focus from other applications.
    /// </summary>
    [InitializeOnLoad]
    public static class FrameworkPlayerValidation
    {
        private const string Pending = "Threadlink.Framework.PlayerValidation";
        private const string Location = Pending + ".Location";
        private const string Started = Pending + ".Started";
        private const string ResultsPath = "Logs/Framework-IL2CPP-player.xml";
        private const string StatusPath = "Logs/Framework-player-status.txt";
        private static Process player;
        private static Action<PlayerOutcome> completed;

        /// <summary>The outcome of one headless player run.</summary>
        public sealed class PlayerOutcome
        {
            /// <summary>Whether the run finished with every test passing.</summary>
            public bool Passed;

            /// <summary>Human-readable summary or failure.</summary>
            public string Message;

            /// <summary>Full names of passed tests.</summary>
            public HashSet<string> PassedTests = new HashSet<string>();

            /// <summary>Counts.</summary>
            public int PassCount, FailCount, SkipCount;
        }

        static FrameworkPlayerValidation() => TestRunnerApi.RegisterTestCallback(new Results());

        [MenuItem("Threadlink/Validation/Framework/Run IL2CPP Player Tests")]
        public static void RunIl2Cpp() => RunIl2Cpp(null);

        /// <summary>Builds and runs the headless IL2CPP test player; <paramref name="onCompleted"/> receives the outcome.</summary>
        internal static void RunIl2Cpp(Action<PlayerOutcome> onCompleted)
        {
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("Select the Standalone IL2CPP backend first.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Pending, false))
                throw new InvalidOperationException("Finish the active Play session or test run first.");
            completed = onCompleted;
            SessionState.SetBool(Pending, true);
            SessionState.SetString(Started, DateTime.UtcNow.ToString("o"));
            SessionState.EraseString(Location);
            Directory.CreateDirectory("Logs");
            File.Delete(ResultsPath);
            File.WriteAllText(StatusPath, "Building IL2CPP test player.");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                api.Execute(
                    new ExecutionSettings(
                        new Filter
                        {
                            testMode = TestMode.PlayMode,
                            targetPlatform = BuildTarget.StandaloneWindows64,
                            assemblyNames = FrameworkValidationPipeline.PlayerAssemblies(),
                        }
                    )
                );
            }
            catch (Exception e)
            {
                Finish(new PlayerOutcome { Message = "Launch failed: " + e });
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(api);
            }
            EditorApplication.update -= WaitForBuild;
            EditorApplication.update += WaitForBuild;
        }

        /// <summary>Continues a pending headless run after a script reload.</summary>
        internal static void Resume(Action<PlayerOutcome> onCompleted)
        {
            if (!SessionState.GetBool(Pending, false))
                return;
            completed = onCompleted;
            EditorApplication.update -= WaitForBuild;
            EditorApplication.update += WaitForBuild;
        }

        /// <summary>
        /// The Test Framework reports no completion for a player it did not start, so the runner watches for the finished
        /// build itself and then starts the player headless.
        /// </summary>
        private static void WaitForBuild()
        {
            if (!SessionState.GetBool(Pending, false) || player != null)
            {
                EditorApplication.update -= WaitForBuild;
                return;
            }
            var started = DateTime.Parse(SessionState.GetString(Started, DateTime.UtcNow.ToString("o"))).ToUniversalTime();
            if (DateTime.UtcNow - started > TimeSpan.FromMinutes(60))
            {
                EditorApplication.update -= WaitForBuild;
                Finish(new PlayerOutcome { Message = "The IL2CPP test player was not built within an hour." });
                return;
            }
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            string exe = SessionState.GetString(Location, string.Empty);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe) || File.GetLastWriteTimeUtc(exe) < started.AddSeconds(-5))
                return;
            EditorApplication.update -= WaitForBuild;
            Launch();
        }

        /// <summary>Removes AutoRunPlayer so the Test Framework only builds; the runner then starts the player headless.</summary>
        public sealed class HeadlessPlayerBuild : ITestPlayerBuildModifier
        {
            public BuildPlayerOptions ModifyOptions(BuildPlayerOptions options)
            {
                if (!SessionState.GetBool(Pending, false))
                    return options;
                options.options &= ~BuildOptions.AutoRunPlayer;
                options.options &= ~BuildOptions.ConnectWithProfiler;
                SessionState.SetString(Location, Path.GetFullPath(options.locationPathName));
                return options;
            }
        }

        private static void Launch()
        {
            string exe = SessionState.GetString(Location, string.Empty);
            var started = DateTime.Parse(SessionState.GetString(Started, DateTime.UtcNow.ToString("o"))).ToUniversalTime();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe) || File.GetLastWriteTimeUtc(exe) < started.AddSeconds(-5))
            {
                Finish(new PlayerOutcome { Message = "The IL2CPP test player was not built." });
                return;
            }
            File.WriteAllText(StatusPath, "Running the headless IL2CPP test player.");
            string results = Path.GetFullPath(ResultsPath);
            player = Process.Start(
                new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"-batchmode -threadlinkResults \"{results}\" -logFile \"{Path.GetFullPath("Logs/Framework-IL2CPP-player.log")}\"",
                    WorkingDirectory = Path.GetDirectoryName(exe),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );
            float deadline = (float)EditorApplication.timeSinceStartup + 1800;
            void Poll()
            {
                bool exited = player == null || player.HasExited;
                if (!exited && EditorApplication.timeSinceStartup < deadline)
                    return;
                EditorApplication.update -= Poll;
                if (!exited)
                {
                    player.Kill();
                    Finish(new PlayerOutcome { Message = "The headless IL2CPP test player timed out." });
                    return;
                }
                Finish(File.Exists(results) ? Read(results) : new PlayerOutcome { Message = $"The headless player exited with code {player.ExitCode} without results." });
            }
            EditorApplication.update += Poll;
        }

        private static PlayerOutcome Read(string path)
        {
            var document = new XmlDocument();
            document.Load(path);
            var outcome = new PlayerOutcome();
            foreach (XmlElement test in document.SelectNodes("//test-case"))
            {
                string result = test.GetAttribute("result");
                if (result == "Passed")
                {
                    outcome.PassCount++;
                    outcome.PassedTests.Add(test.GetAttribute("fullname"));
                }
                else if (result == "Failed")
                    outcome.FailCount++;
                else
                    outcome.SkipCount++;
            }
            outcome.Passed = outcome.FailCount == 0 && outcome.PassCount > 0;
            outcome.Message = $"{(outcome.Passed ? "Passed" : "Failed")}: {outcome.PassCount} passed, {outcome.FailCount} failed, {outcome.SkipCount} skipped.";
            return outcome;
        }

        private static void Finish(PlayerOutcome outcome)
        {
            player = null;
            SessionState.SetBool(Pending, false);
            File.WriteAllText(StatusPath, outcome.Message);
            var callback = completed;
            completed = null;
            callback?.Invoke(outcome);
        }

        private sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                // The Test Framework finishes once the player is built, because it was not auto-run.
                // Completion is detected by WaitForBuild; the Test Framework may not report a run it did not launch.
            }

            public void OnError(string message)
            {
                if (SessionState.GetBool(Pending, false))
                    Finish(new PlayerOutcome { Message = "Test run could not complete: " + message });
            }
        }
    }
}
