﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Faultify.TestRunner.Logging;
using Faultify.TestRunner.Shared;
using Faultify.TestRunner.TestRun;

namespace Faultify.TestRunner
{
    /// <summary>
    ///     Interface for running tests and code coverage on some test host.
    /// </summary>
    public interface ITestHostRunner
    {
        public TestFramework TestFramework { get; }

        /// <summary>
        ///     Runs the given tests and returns the results.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progress"></param>
        /// <param name="tests"></param>
        /// <returns></returns>
        Task<TestResults> RunTests(TimeSpan timeout, IProgress<string> progress,
            IEnumerable<string> tests, IList<MutationVariant> mutationVariants);

        /// <summary>
        ///     Run the code coverage process.
        ///     This process finds out which tests cover which mutations.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progressTracker"></param>
        /// <returns></returns>
        Task<MutationCoverage> RunCodeCoverage(MutationSessionProgressTracker progressTracker, CancellationToken cancellationToken);
    }
}