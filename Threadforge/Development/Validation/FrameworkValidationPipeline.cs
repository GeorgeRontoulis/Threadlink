namespace Threadlink.Framework.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Threadforge.Threadlink.Editor;
    using UnityEditor;
    using UnityEditor.TestTools.TestRunner.Api;
    using UnityEngine;

    /// <summary>Runs authoring, editor/reload, Mono runtime and IL2CPP validation with durable per-stage results.</summary>
    [InitializeOnLoad]
    public static class FrameworkValidationPipeline
    {
        private const string Key = "Threadlink.Framework.Validation.Stage";
        private const string Folder = "Logs/FrameworkValidation";
        private static readonly string[][] Assemblies =
        {
            new[] { "Threadforge.Threadlink.Editor.Tests" },
            new[]
            {
                "Threadforge.Threadlink.Tests",
                "Threadforge.Threadlink.Nexus.Tests",
                "Threadforge.Threadlink.Aura.Tests",
                "Threadforge.Threadlink.Dextra.Tests",
                "Threadforge.Threadlink.Sentinel.Tests",
                "Threadforge.Threadlink.Sentinel.Steam.Tests",
                "Threadforge.Threadlink.Deterministic.Tests",
                "Threadforge.Threadlink.Showcase.Tests",
                "Threadforge.Threadlink.Starter.Tests",
            },
        };
        private const int StageCount = 3;

        /// <summary>Runtime suites built into the IL2CPP player.</summary>
        internal static string[] PlayerAssemblies() => Assemblies[1].Concat(new[] { "Threadlink.Framework.PlayerResults" }).ToArray();
        private static string lastCleanupState;
        private static readonly Func<string, bool> IsRunActive = CreateRunProbe();

        private static Func<string, bool> CreateRunProbe()
        {
            var method = typeof(TestRunnerApi).GetMethod(
                "IsRunning",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            return method == null
                ? null
                : (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), method);
        }

        static FrameworkValidationPipeline()
        {
            TestRunnerApi.RegisterTestCallback(new Results());
            if (SessionState.GetInt(Key, -1) == StageCount - 1)
                FrameworkPlayerValidation.Resume(CompletePlayerStage);
            if (SessionState.GetBool(Key + ".Cleanup", false))
                EditorApplication.update += WaitForCleanup;
        }

        private static void WaitForCleanup()
        {
            if (!SessionState.GetBool(Key + ".Cleanup", false))
            {
                EditorApplication.update -= WaitForCleanup;
                return;
            }
            if (IsRunActive == null)
            {
                EditorApplication.update -= WaitForCleanup;
                Fail("Installed Test Runner does not expose cleanup status.");
                return;
            }
            string previousRun = SessionState.GetString(Key + ".Run", string.Empty);
            bool running = !string.IsNullOrEmpty(previousRun) && IsRunActive(previousRun);
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            string state =
                $"stage={SessionState.GetInt(Key, -1)} run={previousRun} running={running} playing={playing} scene={scene}";
            if (state != lastCleanupState)
            {
                File.AppendAllText(Folder + "/handoff.txt", state + Environment.NewLine);
                lastCleanupState = state;
            }
            if (
                EditorApplication.timeSinceStartup
                    - SessionState.GetFloat(Key + ".CleanupStarted", 0)
                > 120
            )
            {
                Fail(
                    "Test Runner did not restore the original scene within 120 seconds. See handoff.txt."
                );
                return;
            }
            if (
                (running)
                || playing
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || scene != SessionState.GetString(Key + ".Scene", string.Empty)
            )
            {
                SessionState.EraseFloat(Key + ".Stable");
                return;
            }
            float stableSince = SessionState.GetFloat(Key + ".Stable", -1);
            if (stableSince < 0)
            {
                SessionState.SetFloat(Key + ".Stable", (float)EditorApplication.timeSinceStartup);
                return;
            }
            if (EditorApplication.timeSinceStartup - stableSince < 1)
                return;
            EditorApplication.update -= WaitForCleanup;
            SessionState.EraseBool(Key + ".Cleanup");
            StartStage();
        }

        [MenuItem("Threadlink/Validation/Framework/Run All Checks")]
        public static void RunAll() => Run(false);

        /// <summary>Runs all runtime regressions in one Editor Play session without building a player.</summary>
        [MenuItem("Threadlink/Validation/Framework/Run Runtime Checks")]
        public static void RunRuntime() => Run(true);

        /// <summary>
        /// Runs the runtime regressions in Play mode with domain reload disabled, the only mode Unity's CoreCLR Editor
        /// supports, then restores the project's Enter Play Mode settings.
        /// </summary>
        [MenuItem("Threadlink/Validation/Framework/Run Runtime Checks Without Domain Reload")]
        public static void RunRuntimeWithoutDomainReload()
        {
            SessionState.SetBool(Key + ".FastRestore", true);
            SessionState.SetBool(Key + ".FastEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + ".FastOptions", (int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Run(true);
        }

        private static void RestorePlayModeOptions()
        {
            if (!SessionState.GetBool(Key + ".FastRestore", false))
                return;
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + ".FastEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + ".FastOptions", 0);
            SessionState.EraseBool(Key + ".FastRestore");
        }

        private static void Run(bool runtimeOnly)
        {
            if (
                EditorApplication.isPlayingOrWillChangePlaymode
                || SessionState.GetInt(Key, -1) >= 0
            )
                throw new InvalidOperationException(
                    "Finish the current test/Play session before starting framework validation."
                );
            var issues = WorkspaceValidation
                .RunAll()
                .Where(issue => issue.Severity == IssueSeverity.Error)
                .ToList();
            if (issues.Count != 0)
                throw new InvalidOperationException(
                    "Workspace validation failed: " + string.Join("; ", issues)
                );
            Directory.CreateDirectory(Folder);
            foreach (string previousResult in Directory.EnumerateFiles(Folder, "*-results.xml"))
                File.Delete(previousResult);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scene) || !File.Exists(scene))
                throw new InvalidOperationException(
                    "Open a saved scene before running framework validation."
                );
            SessionState.SetString(Key + ".Scene", scene);
            SessionState.SetBool(Key + ".RuntimeOnly", runtimeOnly);
            SessionState.SetInt(Key, runtimeOnly ? 1 : 0);
            StartStage();
        }

        private static void StartStage()
        {
            int stage = SessionState.GetInt(Key, -1);
            if (stage < 0 || SessionState.GetBool(Key + ".Executing", false))
                return;
            SessionState.SetBool(Key + ".Executing", true);
            File.AppendAllText(
                Folder + "/handoff.txt",
                $"START stage={stage} at={EditorApplication.timeSinceStartup}" + Environment.NewLine
            );
            File.WriteAllText(
                Folder + "/status.txt",
                $"Running stage {stage + 1}/{StageCount} with Unity {Application.unityVersion}."
            );
            try
            {
                // Keep the runner API alive across the domain reloads inside Editor integration tests.
                // Destroying it immediately after Execute can invalidate the resumable EditModeRunner.
                foreach (var previous in Resources.FindObjectsOfTypeAll<TestRunnerApi>())
                    if (previous.name == "Threadlink Framework Validation")
                        UnityEngine.Object.DestroyImmediate(previous);
                if (stage == StageCount - 1)
                {
                    File.Copy(
                        Folder + "/2-expected-tests.txt",
                        Folder + "/3-expected-tests.txt",
                        true
                    );
                    FrameworkPlayerValidation.RunIl2Cpp(CompletePlayerStage);
                    return;
                }
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.name = "Threadlink Framework Validation";
                api.hideFlags = HideFlags.HideAndDontSave;
                var mode = stage == 0 ? TestMode.EditMode : TestMode.PlayMode;
                api.RetrieveTestList(
                    mode,
                    root =>
                    {
                        try
                        {
                            var names = new List<string>();
                            var suites = new List<string>();
                            foreach (string assembly in Assemblies[stage])
                            {
                                int before = names.Count;
                                CollectTests(root, assembly, names);
                                if (names.Count == before)
                                    throw new InvalidOperationException(
                                        $"Required test suite is missing or empty: {assembly}."
                                    );
                                suites.Add($"{assembly}: {names.Count - before} tests");
                            }
                            File.WriteAllLines($"{Folder}/{stage + 1}-expected-tests.txt", names);
                            File.WriteAllLines($"{Folder}/{stage + 1}-suites.txt", suites);
                            string run = api.Execute(
                                new ExecutionSettings(
                                    new Filter { testMode = mode, testNames = names.ToArray() }
                                )
                            );
                            SessionState.SetString(Key + ".Run", run);
                            File.AppendAllText(
                                Folder + "/handoff.txt",
                                $"JOB stage={stage} guid={run}" + Environment.NewLine
                            );
                        }
                        catch (Exception error)
                        {
                            Fail(error.ToString());
                        }
                    }
                );
            }
            catch (Exception error)
            {
                Fail(error.ToString());
            }
        }

        private static void CollectTests(ITestAdaptor test, string assembly, List<string> names)
        {
            if (
                test.RunState.ToString() == "Explicit"
                || (
                    test.TypeInfo?.Type?.GetCustomAttributes(true)
                        .Any(attribute => attribute.GetType().Name == "ExplicitAttribute") ?? false
                )
                || (
                    test.Method?.MethodInfo?.GetCustomAttributes(true)
                        .Any(attribute => attribute.GetType().Name == "ExplicitAttribute") ?? false
                )
            )
                return;
            if (
                !test.HasChildren
                && !test.IsSuite
                && test.TypeInfo?.Assembly?.GetName().Name == assembly
            )
                names.Add(test.FullName);
            if (test.Children != null)
                foreach (var child in test.Children)
                    CollectTests(child, assembly, names);
        }

        private static void Fail(string error)
        {
            RestorePlayModeOptions();
            SessionState.EraseInt(Key);
            SessionState.EraseBool(Key + ".Executing");
            SessionState.EraseBool(Key + ".Cleanup");
            EditorApplication.update -= WaitForCleanup;
            File.WriteAllText(Folder + "/status.txt", "FAILED: " + error);
        }

        private static void CollectPassedTests(ITestResultAdaptor result, HashSet<string> names)
        {
            if (!result.Test.IsSuite && result.ResultState == "Passed")
                names.Add(result.Test.FullName);
            if (result.Children != null)
                foreach (var child in result.Children)
                    CollectPassedTests(child, names);
        }

        /// <summary>Completes the IL2CPP stage from the headless player's own results.</summary>
        private static void CompletePlayerStage(FrameworkPlayerValidation.PlayerOutcome outcome)
        {
            if (SessionState.GetInt(Key, -1) != StageCount - 1)
                return;
            SessionState.SetBool(Key + ".Executing", false);
            string player = "Logs/Framework-IL2CPP-player.xml";
            string inner = File.Exists(player) ? File.ReadAllText(player) : string.Empty;
            if (inner.StartsWith("<?xml", StringComparison.Ordinal))
                inner = inner.Substring(inner.IndexOf("?>", StringComparison.Ordinal) + 2);
            string result = outcome.Passed ? "Passed" : "Failed";
            File.WriteAllText(
                $"{Folder}/{StageCount}-results.xml",
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<test-run result=\"{result}\" passed=\"{outcome.PassCount}\" failed=\"{outcome.FailCount}\" skipped=\"{outcome.SkipCount}\" total=\"{outcome.PassCount + outcome.FailCount + outcome.SkipCount}\" engine=\"headless IL2CPP player\">{inner}</test-run>\n"
            );
            var missing = File.ReadAllLines($"{Folder}/{StageCount}-expected-tests.txt").Where(name => !outcome.PassedTests.Contains(name)).ToArray();
            if (!outcome.Passed || missing.Length != 0)
            {
                Fail($"Stage {StageCount}: {outcome.Message} Expected tests without a passing result: {string.Join(", ", missing)}");
                return;
            }
            SessionState.EraseInt(Key);
            File.WriteAllText(Folder + "/status.txt", $"PASSED all three production stages with Unity {Application.unityVersion}.");
        }

        private sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test)
            {
                if (!test.IsSuite && SessionState.GetInt(Key, -1) >= 0)
                    File.WriteAllText(Folder + "/current-test.txt", test.FullName);
            }

            public void TestFinished(ITestResultAdaptor result) { }

            public void OnError(string message)
            {
                if (SessionState.GetInt(Key, -1) >= 0)
                    Fail(message);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int stage = SessionState.GetInt(Key, -1);
                if (stage < 0 || stage == StageCount - 1 || !SessionState.GetBool(Key + ".Executing", false))
                    return;
                SessionState.SetBool(Key + ".Executing", false);
                TestRunnerApi.SaveResultToFile(
                    result,
                    Path.GetFullPath($"{Folder}/{stage + 1}-results.xml")
                );
                var passed = new HashSet<string>();
                CollectPassedTests(result, passed);
                var missing = File.ReadAllLines($"{Folder}/{stage + 1}-expected-tests.txt")
                    .Where(name => !passed.Contains(name))
                    .ToArray();
                if (result.ResultState != "Passed" || missing.Length != 0)
                {
                    Fail(
                        $"Stage {stage + 1}: {result.ResultState}, {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped. Expected tests without a passing result: {string.Join(", ", missing)}"
                    );
                    return;
                }
                if (stage == StageCount - 1 || SessionState.GetBool(Key + ".RuntimeOnly", false))
                {
                    bool withoutReload = SessionState.GetBool(Key + ".FastRestore", false);
                    RestorePlayModeOptions();
                    SessionState.EraseInt(Key);
                    File.WriteAllText(
                        Folder + "/status.txt",
                        SessionState.GetBool(Key + ".RuntimeOnly", false)
                            ? $"PASSED runtime checks{(withoutReload ? " without domain reload" : string.Empty)} with Unity {Application.unityVersion}."
                            : $"PASSED all three production stages with Unity {Application.unityVersion}."
                    );
                    return;
                }
                SessionState.SetInt(Key, stage + 1);
                SessionState.SetFloat(
                    Key + ".CleanupStarted",
                    (float)EditorApplication.timeSinceStartup
                );
                SessionState.EraseFloat(Key + ".Stable");
                SessionState.SetBool(Key + ".Cleanup", true);
                EditorApplication.update -= WaitForCleanup;
                EditorApplication.update += WaitForCleanup;
            }
        }
    }
}
