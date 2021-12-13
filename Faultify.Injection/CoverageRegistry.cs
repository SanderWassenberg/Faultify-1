using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using Faultify.TestRunner.Shared;

namespace Faultify.Injection
{
    /// <summary>
    ///     Registry that tracks which tests cover which entity handled.
    ///     This information is used by the test runner to know which mutations can be ran in parallel.
    /// </summary>
    public static class CoverageRegistry
    {
        private static readonly MutationCoverage MutationCoverage = new();
        private static bool _runningTest = false;
        private static string _currentTest;
        private static readonly object RegisterMutex = new();
        private static MemoryMappedFile _mmf;

        /// <summary>
        ///     Is injected into <Module> by <see cref="TestCoverageInjector" /> and will be called on assembly load.
        /// </summary>
        public static void Initialize()
        {
            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomain_ProcessExit;
            _mmf = MemoryMappedFile.OpenExisting("CoverageFile", MemoryMappedFileRights.ReadWrite);
        }

        private static void OnCurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            try
            {
                Utils.WriteMutationCoverageFile(MutationCoverage, _mmf);
            }
            catch (Exception _)
            {
                // Ignored. If we can't write to the coverage file then that's fine for now.
                // Its content is checked back in the Faultify process.
            }
        }

        /// <summary>
        ///     Registers the given method entity handle as 'covered' by the last registered 'test'
        /// </summary>
        /// <param name="entityHandle"></param>
        public static void RegisterTargetCoverage(string assemblyName, int entityHandle)
        {
            lock (RegisterMutex)
            {
                if (!_runningTest) return;

                if (!MutationCoverage.Coverage.TryGetValue(_currentTest, out var targetHandles))
                {
                    targetHandles = new List<RegisteredCoverage>();
                    MutationCoverage.Coverage[_currentTest] = targetHandles;
                }

                targetHandles.Add(new RegisteredCoverage(assemblyName, entityHandle));
            }
        }

        /// <summary>
        ///     Marks the beginning of the current test.
        /// </summary>
        /// <param name="testName"></param>
        public static void BeginRegisterTestCoverage(string testName)
        {
            _runningTest = true;
            _currentTest = testName;
        }

        /// <summary>
        ///     Marks the end of the current test.
        /// </summary>
        public static void EndRegisterTestCoverage()
        {
            _runningTest = false;
        }
    }
}