﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Faultify.TestRunner.Logging;
using Faultify.TestRunner.Shared;
using Faultify.TestRunner.TestProcess;
using Faultify.TestRunner.TestRun;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using TestResult = Faultify.TestRunner.Shared.TestResult;

namespace Faultify.TestRunner.Dotnet
{
    /// <summary>
    ///     Runs the mutation test with 'dotnet test'.
    /// </summary>
    public class DotnetTestHostRunner : ITestHostRunner
    {
        private static readonly bool DisableOutput = true;
        private readonly ILogger _logger;
        private readonly string _testAdapterPath;
        private readonly DirectoryInfo _testDirectoryInfo;

        private readonly FileInfo _testFileInfo;
        private readonly TimeSpan _timeout;

        public DotnetTestHostRunner(string testProjectAssemblyPath, TimeSpan timeout, ILogger logger)
        {
            _testFileInfo = new FileInfo(testProjectAssemblyPath);
            _testDirectoryInfo = new DirectoryInfo(_testFileInfo.DirectoryName ?? string.Empty);

            _timeout = timeout;
            _logger = logger;
            _testAdapterPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        }

        public TestFramework TestFramework => TestFramework.DotNet;

        /// <summary>
        ///     Runs the given tests and returns the results.
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="tests"></param>
        /// <returns></returns>
        public async Task<TestResults> RunTests(TimeSpan timeout, IProgress<string> progress,
            IEnumerable<string> tests, IList<MutationVariant> mutationVariants)
        {
            var testResultOutputPath = Path.Combine(_testDirectoryInfo.FullName, TestRunnerConstants.TestsFileName);

            var testResults = new List<TestResult>();
            var remainingTests = new HashSet<string>(tests);

            while (remainingTests.Any())
                try
                {
                    var testProcessRunner = BuildTestProcessRunner(remainingTests);

                    await testProcessRunner.RunAsync();
                    _logger.LogDebug(testProcessRunner.Output.ToString());
                    _logger.LogError(testProcessRunner.Error.ToString());

                    var testResultsBinary = await File.ReadAllBytesAsync(testResultOutputPath,
                        new CancellationTokenSource(timeout).Token);

                    var deserializedTestResults = TestResults.Deserialize(testResultsBinary, true);
                    
                    //if test result is none(Time-out) remove all tests with exactly the same mutation on the same part in the code. Removes unnecessary tests
                    if (deserializedTestResults.Tests[0].Outcome == TestOutcome.None)
                    {
                        foreach (var variant in mutationVariants)
                        {
                            if (variant.MutationIdentifier.TestCoverage.Contains(deserializedTestResults.Tests[0].Name))
                            {
                                foreach (var test in variant.MutationIdentifier.TestCoverage)
                                {
                                    testResults.Add(new TestResult(){Guid = Guid.NewGuid(), Name = test, Outcome = TestOutcome.None});
                                    remainingTests.Remove(test);
                                }
                            }
                        }
                    }
                    else
                    {
                        remainingTests.RemoveWhere(x => deserializedTestResults.Tests.Any(y => y.Name == x));

                        foreach (var testResult in deserializedTestResults.Tests) testResults.Add(testResult);
                    }


                    
                }
                catch (FileNotFoundException)
                {
                    _logger.LogError(
                        "The file 'test_results.bin' was not generated." +
                        "This implies that the test run can not be completed. " +
                        "Consider opening up an issue with the logs found in the output folder."
                    );
                }
                catch (Exception e)
                {
                    _logger.LogError(e.ToString());
                }
                finally
                {
                    if (File.Exists(testResultOutputPath))
                    {
                        File.Delete(testResultOutputPath);
                    }
                }

            return new TestResults {Tests = testResults};
        }

        /// <summary>
        ///     Run the code coverage process.
        ///     This process finds out which tests cover which mutations.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<MutationCoverage> RunCodeCoverage(MutationSessionProgressTracker progressTracker, CancellationToken cancellationToken)
        {
            var coverageProcessRunner = BuildCodeCoverageTestProcessRunner();
            var process = await coverageProcessRunner.RunAsync();

            var output = coverageProcessRunner.Output.ToString();
            var errorOutput = coverageProcessRunner.Error.ToString();

            _logger.LogDebug(output);
            _logger.LogError(errorOutput);

            if (process.ExitCode != 0)
            {
                if (errorOutput.Contains("The active test run was aborted."))
                {
                    progressTracker.LogCriticalErrorAndExit($"Coverage calculation failed. Please be sure your tests succeed by default.\n--- test host error output ---\n{errorOutput.Trim()}");
                }
                
                if (Regex.IsMatch(output, @"----> System\.IO\.FileNotFoundException : (.*)netstandard(.*)\n"))
                {
                    progressTracker.LogCriticalErrorAndExit("Trying to mutate a .NET Framework project. Faultify currently doesn't support Framework projects.");
                }

                var failedTestRegex = new Regex(".*Failed (.*) \\[.*");
                if (failedTestRegex.IsMatch(output))
                {
                    List<string> failedTests = new List<string>();
                    var myCapturedText = failedTestRegex.Matches(output);
                    foreach (Match item in myCapturedText)
                    {
                        failedTests.Add(item.Groups[1].Value);
                    }

                    progressTracker.LogTestFailedAndExit(failedTests);
                }

                progressTracker.LogCriticalErrorAndExit($"Subprocess terminated with error code {process.ExitCode}");
            }

            try
            {
                return Utils.ReadMutationCoverageFile();
            }
            catch (FileNotFoundException e)
            {
                progressTracker.LogCriticalErrorAndExit("The file 'coverage.bin' was not generated."
                    + "This implies that the test run can not be completed. ");

                // The statement above already terminates the program,
                // but this is so that the compiler wont complain about non-returning code paths.
                return null;
            }
        }

        /// <summary>
        ///     Constructs an instance of a <see cref="ProcessRunner" /> for the test process.
        /// </summary>
        /// <param name="tests"></param>
        /// <returns></returns>
        private ProcessRunner BuildTestProcessRunner(IEnumerable<string> tests)
        {
            var testArguments = new DotnetTestArgumentBuilder(_testFileInfo.Name)
                .Silent()
                .WithoutLogo()
                .WithTimeout(_timeout)
                .WithTestAdapter(_testAdapterPath)
                .WithCollector("TestDataCollector")
                .WithTests(tests)
                .DisableDump()
                .Build();

            var testProcessStartInfo = new ProcessStartInfo("dotnet", testArguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _testDirectoryInfo.FullName,
                RedirectStandardOutput = DisableOutput,
                RedirectStandardError = DisableOutput
            };

            _logger.LogDebug($"Test process process arguments: {testArguments}");

            return new ProcessRunner(testProcessStartInfo);
        }

        /// <summary>
        ///     Constructs an instance of a <see cref="ProcessRunner" /> for the coverage test process.
        /// </summary>
        /// <returns></returns>
        private ProcessRunner BuildCodeCoverageTestProcessRunner()
        {
            var coverageArguments = new DotnetTestArgumentBuilder(_testFileInfo.Name)
                .Silent()
                .WithoutLogo()
                .WithTimeout(_timeout)
                .WithTestAdapter(_testAdapterPath)
                .WithCollector("CoverageDataCollector")
                .DisableDump()
                .Build();

            var coverageProcessStartInfo = new ProcessStartInfo("dotnet", coverageArguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = DisableOutput,
                RedirectStandardError = DisableOutput,
                WorkingDirectory = _testDirectoryInfo.FullName
            };

            _logger.LogDebug($"Coverage test process arguments: {coverageArguments}");

            return new ProcessRunner(coverageProcessStartInfo);
        }
    }
}
