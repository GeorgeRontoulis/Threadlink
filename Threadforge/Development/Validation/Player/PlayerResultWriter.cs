[assembly: UnityEngine.TestRunner.TestRunCallback(typeof(Threadlink.Framework.PlayerResults.PlayerResultWriter))]

namespace Threadlink.Framework.PlayerResults
{
    using System;
    using System.IO;
    using NUnit.Framework.Interfaces;
    using UnityEngine;
    using UnityEngine.TestRunner;

    /// <summary>
    /// Writes the NUnit result of a test player run to the path after <c>-threadlinkResults</c>, then quits. This lets the
    /// validation runner start the test player headless, with no window, and read its results from disk.
    /// </summary>
    public sealed class PlayerResultWriter : ITestRunCallback
    {
        /// <summary>Command-line switch that names the result file.</summary>
        public const string Argument = "-threadlinkResults";

        public void RunStarted(ITest testsToRun) { }

        public void RunFinished(ITestResult testResults)
        {
            if (Application.isEditor)
                return;
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, Argument);
            if (index < 0 || index + 1 >= arguments.Length)
                return;
            string path = arguments[index + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path + ".tmp", testResults.ToXml(true).OuterXml);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(path + ".tmp", path);
            Application.Quit(0);
        }

        public void TestStarted(ITest test) { }

        public void TestFinished(ITestResult result) { }
    }
}
