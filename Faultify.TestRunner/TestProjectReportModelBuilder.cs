using System;
using System.Collections.Generic;
using System.Linq;
using Faultify.Report;
using Faultify.TestRunner.Shared;
using Faultify.TestRunner.TestRun;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using TestResult = Faultify.TestRunner.Shared.TestResult;

namespace Faultify.TestRunner
{
    public class TestProjectReportModelBuilder
    {
        private static readonly object Mutext = new();
        private readonly TestProjectReportModel _testProjectReportModel;

        public TestProjectReportModelBuilder(string testProjectName)
        {
            _testProjectReportModel = new TestProjectReportModel(testProjectName, TimeSpan.MaxValue);
        }

        public void AddTestResult(TestResults testResults, IEnumerable<MutationVariant> mutations,
            TimeSpan testRunDuration)
        {
            if (testResults == null) return;
            if (mutations == null) return;

            lock (Mutext)
            {
                foreach (var mutation in mutations)
                {
                    // if there is no mutation or if the mutation has already been added to the report, skip the entry
                    if (mutation?.Mutation == null ||
                        _testProjectReportModel.Mutations.Any(x =>
                            x.MutationId == mutation.MutationIdentifier.MutationId &&
                            x.MemberName == mutation.MutationIdentifier.MemberName))
                    {
                        continue;
                    }

                    // get all the tests that cover a mutation
                    var allTestsForMutation = new List<TestResult>();
                    foreach (var testNameInCoverage in mutation.MutationIdentifier.TestCoverage)
                    {
                        // FindAll instead of Find because NUnit TestCases (or XUnit/MSTest equivalent) have the same name, they need to all be included.
                        var matchingTestResults = testResults.Tests.FindAll(t => t.Name == testNameInCoverage);
                        allTestsForMutation.AddRange(matchingTestResults);
                    }

                    // if there are no tests covering the mutation, mark it with no coverage
                    // otherwise, determine the success based on the outcome of the tests
                    var mutationStatus = allTestsForMutation.Count == 0 ? MutationStatus.NoCoverage : GetMutationStatus(allTestsForMutation);

                    // Adds a highlight (comment) to the orignal and mutated source
                    var mutationHighlighted = HighlightMutation(mutation);

                    // Add mutation to the report
                    _testProjectReportModel.Mutations.Add(new MutationVariantReportModel(
                        mutation.Mutation.Report, "",
                        new MutationAnalyzerReportModel(
                            mutation.MutationAnalyzerInfo.AnalyzerName,
                            mutation.MutationAnalyzerInfo.AnalyzerDescription),
                        mutationStatus,
                        testRunDuration,
                        mutationHighlighted.OriginalSource,
                        mutationHighlighted.MutatedSource,
                        mutation.MutationIdentifier.MutationId,
                        mutation.MutationIdentifier.MemberName,
                        mutationStatus == MutationStatus.Survived ? allTestsForMutation.Select(x => x.Name).ToList() : new List<string>()
                    ));
                }
            }
        }

        public TestProjectReportModel Build(TimeSpan testDuration, int totalTestRuns)
        {
            _testProjectReportModel.InitializeMetrics(totalTestRuns, testDuration);
            return _testProjectReportModel;
        }

        private MutationStatus GetMutationStatus(List<TestResult> testResultsTests)
        {
            // if all tests have passed, the mutation wasn't caught (it survived)
            if (testResultsTests.All(t => t.Outcome == TestOutcome.Passed)) return MutationStatus.Survived;

            // if any of the tests have failed, the mutation was caught (it was killed)
            if (testResultsTests.Any(t => t.Outcome == TestOutcome.Failed)) return MutationStatus.Killed;

            // any other outcome is being marked as a timeout 
            return MutationStatus.Timeout;
        }

        private MutationVariant HighlightMutation(MutationVariant mutation)
        {
            // Gets the index of the mutation
            int mutationIndex = mutation.OriginalSource.Zip(mutation.MutatedSource, (c1, c2) => c1 == c2).TakeWhile(b => b).Count() + 1;

            // Gets the index of the last occurrence of \r\n starting from the mutationIndex and going backwards
            int insertIndex = mutation.OriginalSource.LastIndexOf("\r\n", mutationIndex);
            mutation.OriginalSource = mutation.OriginalSource.Insert(insertIndex, "\r\n//This will be mutated");
            mutation.MutatedSource = mutation.MutatedSource.Insert(insertIndex, "\r\n//This is the mutation");

            return mutation;
        }
    }
}