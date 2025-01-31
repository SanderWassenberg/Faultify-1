﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Faultify.TestRunner.ProjectDuplication
{
    /// <summary>
    ///     A pool that grands access to a test project that can be used for mutation testing.
    /// </summary>
    public class TestProjectDuplicationPool
    {
        private static readonly object Lock = new();
        private readonly AutoResetEvent _signalEvent = new(false);
        private readonly List<TestProjectDuplication> _testProjectDuplications;

        public TestProjectDuplicationPool(List<TestProjectDuplication> duplications)
        {
            _testProjectDuplications = duplications;

            foreach (var testProjectDuplication in _testProjectDuplications)
                testProjectDuplication.TestProjectFreed += OnTestProjectFreed;
        }

        /// <summary>
        ///     Takes and removes a test project from the pool
        /// </summary>
        /// <returns></returns>
        public TestProjectDuplication TakeTestProject()
        {
            var first = _testProjectDuplications.FirstOrDefault();
            if (first == null) return null;

            _testProjectDuplications.RemoveAt(0);
            return first;
        }

        /// <summary>
        ///     Acquire a test project or wait until one is released.
        ///     This will hang until test projects are freed.
        /// </summary>
        /// <returns></returns>
        public TestProjectDuplication AcquireTestProject()
        {
            // Make sure only one thread can attempt to access a free project at a time.
            lock (Lock)
            {
                var freeProject = GetFreeProject();

                if (freeProject != null) return freeProject;

                _signalEvent.WaitOne();

                freeProject = GetFreeProject();

                if (freeProject != null)
                    return freeProject;

                return AcquireTestProject(); // Why is this here? Running this line WILL cause the current thread to become deadlocked. The mutex is already locked, entering this method again recursively will mean it never gets past the lock.
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement#guidelines
            }
        }

        /// <summary>
        ///     Returns a free project or null if none exit.
        /// </summary>
        /// <returns></returns>
        public TestProjectDuplication GetFreeProject()
        {
            foreach (var testProjectDuplication in _testProjectDuplications)
                if (!testProjectDuplication.IsInUse)
                {
                    testProjectDuplication.IsInUse = true;
                    return testProjectDuplication;
                }

            return null;
        }

        /// <summary>
        ///     Signal that a test test project is freed.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="_"></param>
        private void OnTestProjectFreed(object e, TestProjectDuplication _)
        {
            _signalEvent.Set();
        }
    }
}