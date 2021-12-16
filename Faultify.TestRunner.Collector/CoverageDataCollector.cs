using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Faultify.TestRunner.Shared;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;

namespace Faultify.TestRunner.Collector
{
    /// <summary>
    ///     Logs information as the coverage is being calculated.
    /// </summary>
    [DataCollectorFriendlyName("CoverageDataCollector")]
    [DataCollectorTypeUri("my://coverage/datacollector")]
    public class CoverageDataCollector : DataCollector
    {
        private DataCollectionLogger _logger;
        private DataCollectionEnvironmentContext context;

        public override void Initialize(
            XmlElement configurationElement,
            DataCollectionEvents events,
            DataCollectionSink dataSink,
            DataCollectionLogger logger,
            DataCollectionEnvironmentContext environmentContext)
        {
            _logger = logger;
            context = environmentContext;

            events.TestCaseEnd += EventsOnTestCaseEnd;
            events.TestCaseStart += EventsOnTestCaseStart;

            events.SessionEnd += EventsOnSessionEnd;
            events.SessionStart += EventsOnSessionStart;

            AppDomain.CurrentDomain.ProcessExit += OnCurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomain_ProcessExit;
        }

        private void EventsOnSessionStart(object sender, SessionStartEventArgs e)
        {
            _logger.LogWarning(context.SessionDataCollectionContext, "Coverage Test Session Started");
        }

        private void OnCurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _logger.LogWarning(context.SessionDataCollectionContext, "Coverage Test Session Exit");

            EventsOnSessionEnd(sender, new SessionEndEventArgs());
        }

        private void EventsOnSessionEnd(object sender, SessionEndEventArgs e)
        {
            _logger.LogWarning(context.SessionDataCollectionContext, "Coverage Test Session Finished");
        }

        private void EventsOnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
            _logger.LogWarning(context.SessionDataCollectionContext, $"Test Case Start: {e.TestCaseName}");
        }

        private void EventsOnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
            _logger.LogWarning(context.SessionDataCollectionContext, $"Test Case End: {e.TestCaseName}");
        }
    }
}