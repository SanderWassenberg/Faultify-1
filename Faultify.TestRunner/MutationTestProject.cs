using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Faultify.Analyze;
using Faultify.Analyze.AssemblyMutator;
using Faultify.Core.ProjectAnalyzing;
using Faultify.Injection;
using Faultify.Report;
using Faultify.TestRunner.Logging;
using Faultify.TestRunner.ProjectDuplication;
using Faultify.TestRunner.Shared;
using Faultify.TestRunner.TestRun;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace Faultify.TestRunner
{
    public class MutationTestProject
    {
        private readonly MutationLevel _mutationLevel;
        private readonly int _parallel;
        private readonly ILogger _testHostLogger;
        private readonly ITestHostRunFactory _testHostRunFactory;
        private readonly string _testProjectPath;
        private readonly TimeSpan _timeOut;

        public MutationTestProject(string testProjectPath, MutationLevel mutationLevel, int parallel,
            ILoggerFactory loggerFactoryFactory, ITestHostRunFactory testHostRunFactory, TimeSpan timeOut)
        {
            _testProjectPath = testProjectPath;
            _mutationLevel = mutationLevel;
            _parallel = parallel;
            _testHostRunFactory = testHostRunFactory;
            _timeOut = timeOut;
            _testHostLogger = loggerFactoryFactory.CreateLogger("Faultify.TestHost");
        }


        /// <summary>
        ///     Executes the mutation test session for the given test project.
        ///     Algorithm:
        ///     1. Build project
        ///     2. Calculate for each test which mutations they cover.
        ///     3. Rebuild to remove injected code.
        ///     4. Run test session.
        ///     4a. Generate optimized test runs.
        ///     5. Build report.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<TestProjectReportModel> Test(MutationSessionProgressTracker progressTracker,
            CancellationToken cancellationToken = default)
        {
            // Build project
            progressTracker.LogBeginPreBuilding();
            var projectInfo = await BuildProject(progressTracker, _testProjectPath);
            progressTracker.LogEndPreBuilding();

            // Check if the build succeeded
            if (!File.Exists(projectInfo.AssemblyPath))
            {
                progressTracker.LogCriticalErrorAndExit("Couldn't build the unit-testproject. Please check for build errors.");
            }

            // Copy project N times
            var testProjectCopier = new TestProjectDuplicator(Directory.GetParent(projectInfo.AssemblyPath).FullName);

            testProjectCopier.MakeInitialCopy(projectInfo);

            // Begin code coverage on first project.
            var coverageProject = testProjectCopier.MakeCopy(1);
            var coverageProjectInfo = GetTestProjectInfo(coverageProject, projectInfo, progressTracker);

            // Measure the test coverage 
            progressTracker.LogBeginCoverage();
            PrepareAssembliesForCodeCoverage(coverageProjectInfo);

            var coverageTimer = Stopwatch.StartNew();
            var coverage = await RunCoverage(progressTracker, coverageProject.TestProjectFile.FullFilePath(), cancellationToken);
            coverageTimer.Stop();

            if (coverage.Coverage.Count <= 0)
            {
                progressTracker.LogCriticalErrorAndExit("Found 0 test-coverage, terminating. Either the unit-tests don't test any methods, or something went wrong whilst calculating the coverage.");
            }

            var timeout = _createTimeOut(coverageTimer);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            coverageProject.MarkAsFree();

            // Start test session.
            var testsPerMutation = GroupMutationsWithTests(coverage);
            return StartMutationTestSession(coverageProjectInfo, testsPerMutation, progressTracker,
                timeout, testProjectCopier);
        }

        /// <summary>
        ///     Returns information about the test project.
        /// </summary>
        /// <returns></returns>
        private TestProjectInfo GetTestProjectInfo(TestProjectDuplication duplication, IProjectInfo testProjectInfo, MutationSessionProgressTracker progressTracker)
        {
            var testFramework = GetTestFramework(testProjectInfo);

            // Read the test project into memory.
            var projectInfo = new TestProjectInfo
            {
                TestFramework = testFramework,
                TestModule = ModuleDefinition.ReadModule(duplication.TestProjectFile.FullFilePath())
            };

            // Foreach project reference load it in memory as an 'assembly mutator'.
            foreach (var projectReferencePath in duplication.TestProjectReferences)
            {
                string referencePath = projectReferencePath.FullFilePath();
                if (!File.Exists(referencePath))
                {
                    progressTracker.LogCriticalErrorAndExit($"Couldn't find class library \"{projectReferencePath.Name}\", please check project output settings.");
                }

                var loadProjectReferenceModel = new AssemblyMutator(referencePath);

                if (loadProjectReferenceModel.Types.Count > 0)
                    projectInfo.DependencyAssemblies.Add(loadProjectReferenceModel);
            }

            return projectInfo;
        }

        private TestFramework GetTestFramework(IProjectInfo projectInfo)
        {
            var projectFile = File.ReadAllText(projectInfo.ProjectFilePath);

            if (Regex.Match(projectFile, "xunit").Captures.Any())
                return TestFramework.XUnit;

            if (Regex.Match(projectFile, "nunit").Captures.Any())
                return TestFramework.NUnit;

            if (Regex.Match(projectFile, "mstest").Captures.Any())
                return TestFramework.MsTest;

            return TestFramework.None;
        }


        /// <summary>
        ///     Builds the project at the given project path.
        /// </summary>
        /// <param name="sessionProgressTracker"></param>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        private async Task<IProjectInfo> BuildProject(MutationSessionProgressTracker sessionProgressTracker,
            string projectPath)
        {
            var projectReader = new ProjectReader();
            return await projectReader.ReadProjectAsync(projectPath, sessionProgressTracker);
        }

        /// <summary>
        ///     Injects the test project and all mutation assemblies with coverage injection code.
        ///     This code that is injected will register calls to methods/tests.
        ///     Those calls determine what tests cover which methods.
        /// </summary>
        /// <param name="projectInfo"></param>
        private void PrepareAssembliesForCodeCoverage(TestProjectInfo projectInfo)
        {
            TestCoverageInjector.Instance.InjectTestCoverage(projectInfo.TestModule);
            TestCoverageInjector.Instance.InjectModuleInit(projectInfo.TestModule);
            TestCoverageInjector.Instance.InjectAssemblyReferences(projectInfo.TestModule);

            using var ms = new MemoryStream();
            projectInfo.TestModule.Write(ms);
            projectInfo.TestModule.Dispose();

            File.WriteAllBytes(projectInfo.TestModule.FileName, ms.ToArray());

            foreach (var assembly in projectInfo.DependencyAssemblies)
            {
                TestCoverageInjector.Instance.InjectAssemblyReferences(assembly.Module);
                TestCoverageInjector.Instance.InjectTargetCoverage(assembly.Module);
                assembly.Flush();
                assembly.Dispose();
            }

            if (projectInfo.TestFramework == TestFramework.XUnit)
            {
                var testDirectory = new FileInfo(projectInfo.TestModule.FileName).Directory;
                var xunitConfigFileName = Path.Combine(testDirectory.FullName, "xunit.runner.json");
                var xunitCoverageSettings = JObject.FromObject(new { parallelizeTestCollections = false });
                if (!File.Exists(xunitConfigFileName))
                {
                    File.WriteAllText(xunitConfigFileName, xunitCoverageSettings.ToString());
                }
                else
                {
                    var originalJsonConfig = JObject.Parse(File.ReadAllText(xunitConfigFileName));
                    originalJsonConfig.Merge(xunitCoverageSettings);
                    File.WriteAllText(xunitConfigFileName, originalJsonConfig.ToString());
                }
            }
        }

        /// <summary>
        ///     Runs the coverage process.
        ///     This process will get all tests with the methods covered by that test.
        /// </summary>
        /// <param name="testAssemblyPath"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<MutationCoverage> RunCoverage(MutationSessionProgressTracker progressTracker, string testAssemblyPath, CancellationToken cancellationToken)
        {
            using var file = Utils.CreateCoverageMemoryMappedFile();
            try
            {
                var testRunner =
                    _testHostRunFactory.CreateTestRunner(testAssemblyPath, TimeSpan.FromSeconds(12), _testHostLogger);
                return await testRunner.RunCodeCoverage(progressTracker, cancellationToken);
            }
            catch (Exception e)
            {
                _testHostLogger.LogError(e, "Unable to create Test Coverage Runner");
                progressTracker.LogCriticalErrorAndExit("Unable to create Test Coverage Runner");
                return null; // above statment exits the process already
            }
        }

        /// <summary>
        ///     Groups methods with all tests which cover those methods.
        /// </summary>
        /// <param name="coverage"></param>
        /// <returns></returns>
        private Dictionary<RegisteredCoverage, HashSet<string>> GroupMutationsWithTests(MutationCoverage coverage)
        {
            // Group mutations with tests.
            var testsPerMutation = new Dictionary<RegisteredCoverage, HashSet<string>>();
            foreach (var (testName, mutationIds) in coverage.Coverage)
            foreach (var registeredCoverage in mutationIds)
            {
                if (!testsPerMutation.TryGetValue(registeredCoverage, out var testNames))
                {
                    testNames = new HashSet<string>();
                    testsPerMutation.Add(registeredCoverage, testNames);
                }

                testNames.Add(testName);
            }

            return testsPerMutation;
        }

        /// <summary>
        ///     Starts the mutation test session and returns the report with results.
        /// </summary>
        /// <param name="testProjectInfo"></param>
        /// <param name="testsPerMutation"></param>
        /// <param name="sessionProgressTracker"></param>
        /// <param name="coverageTestRunTime"></param>
        /// <param name="testProjectDuplicationPool"></param>
        /// <returns></returns>
        private TestProjectReportModel StartMutationTestSession(TestProjectInfo testProjectInfo,
            Dictionary<RegisteredCoverage, HashSet<string>> testsPerMutation,
            MutationSessionProgressTracker sessionProgressTracker, TimeSpan coverageTestRunTime,
            TestProjectDuplicator testProjectDuplicator)
        {
            // Generate the mutation test runs for the mutation session.
            var defaultMutationTestRunGenerator = new DefaultMutationTestRunGenerator();
            var runs = defaultMutationTestRunGenerator.GenerateMutationTestRuns(testsPerMutation, testProjectInfo,
                _mutationLevel);

            // Set the time the code coverage took such that test runs have some time run their tests (needs to be in seconds).
            var maxTestDuration = TimeSpan.FromSeconds(Math.Round(coverageTestRunTime.TotalMilliseconds / 1000));

            var reportBuilder = new TestProjectReportModelBuilder(testProjectInfo.TestModule.Name);

            var allRunsStopwatch = new Stopwatch();
            allRunsStopwatch.Start();

            var mutationTestRuns = runs.ToList();
            var totalRunsCount = mutationTestRuns.Count();
            var mutationCount = mutationTestRuns.Sum(x => x.MutationCount);
            var completedRuns = 0;
            var failedRuns = 0;

            sessionProgressTracker.LogBeginTestSession(totalRunsCount, mutationCount, maxTestDuration);

            // Stores timed out mutations which will be excluded from test runs if they occur. 
            // Timed out mutations will be removed because they can cause serious test delays.
            var timedOutMutations = new List<MutationVariantIdentifier>();

            async Task RunTestRun(IMutationTestRun testRun)
            {
                var testProject = testProjectDuplicator.MakeCopy(testRun.RunId + 2);

                try
                {
                    testRun.InitializeMutations(testProject, timedOutMutations);

                    var singRunsStopwatch = new Stopwatch();
                    singRunsStopwatch.Start();

                    var results = await testRun.RunMutationTestAsync(maxTestDuration, sessionProgressTracker,
                        _testHostRunFactory, testProject, _testHostLogger);

                    if (results != null)
                    {
                        foreach (var testResult in results)
                        {
                            // Store the timed out mutations such that they can be excluded.
                            timedOutMutations.AddRange(testResult.GetTimedOutTests());

                            // For each mutation that does not cause a timeout add it to the report builder.
                            reportBuilder.AddTestResult(testResult.TestResults, testResult.Mutations.Where(m => !m.CausesTimeOut),
                                singRunsStopwatch.Elapsed);
                        }

                        singRunsStopwatch.Stop();
                        singRunsStopwatch.Reset();
                    }
                }
                catch (Exception e)
                {
                    _testHostLogger.LogError(e, "The test process encountered an unexpected error.");
                    sessionProgressTracker.Log(
                        $"The test process encountered an unexpected error. Continuing without this test run. Please consider to submit an github issue. {e}",
                        LogMessageType.Error);
                    failedRuns += 1;
                }
                finally
                {
                    lock (this) // Probably shouldn't do that, locking 'this' can be dangerous. If within this block, you call a method that ALSO locks 'this', the current thread becomes deadlocked. Here's some guidelines: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement#guidelines
                    {
                        completedRuns += 1;
                        sessionProgressTracker.LogTestRunUpdate(completedRuns, totalRunsCount, failedRuns);
                    }

                    testProject.MarkAsFree();
                }
            }

            // Starts each testrun asynchronously and put all tasks into a list. Then wait until all tasks are done.
            // The RunTestRun method also adds the results of each test to the report.
            var tasks = mutationTestRuns.Select(testRun => RunTestRun(testRun));
            Task.WaitAll(tasks.ToArray());

            allRunsStopwatch.Stop();

            var report = reportBuilder.Build(allRunsStopwatch.Elapsed, totalRunsCount);
            sessionProgressTracker.LogEndTestSession(allRunsStopwatch.Elapsed, completedRuns, mutationCount,
                report.ScorePercentage);

            return report;
        }

        // Sets the time out for the mutations to be either the specified number of seconds or the time it takes to run the test project.
        // When timeout is less then 0.51 seconds it will be set to .51 seconds to make sure the MaxTestDuration is at least one second.
        private TimeSpan _createTimeOut(Stopwatch stopwatch)
        {
            var timeOut = _timeOut;
            if (_timeOut.Equals(TimeSpan.FromSeconds(0))) timeOut = stopwatch.Elapsed;

            return timeOut < TimeSpan.FromSeconds(.51) ? TimeSpan.FromSeconds(.51) : timeOut;
        }
    }
}