using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Faultify.Report;
using Faultify.Report.HTMLReporter;
using Faultify.Report.PDFReporter;
using Faultify.TestRunner;
using Faultify.TestRunner.Dotnet;
using Faultify.TestRunner.Logging;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faultify.Cli
{

    internal class Program
    {
        private static string _outputDirectory;
        private readonly ILoggerFactory _loggerFactory;
        private static (int, int) cursorPosition = (0, 0);

        public Program(
            IOptions<Settings> _,
            ILoggerFactory loggerFactory
        )
        {
            _loggerFactory = loggerFactory;
        }

        private static async Task Main(string[] args)
        {
            var settings = ParseCommandlineArguments(args);

            var currentDate = DateTime.Now.ToString("yy-MM-dd");
            _outputDirectory = Path.Combine(settings.ReportPath, currentDate);
            Directory.CreateDirectory(_outputDirectory);

            var configurationRoot = BuildConfigurationRoot();
            var services = new ServiceCollection();
            services.Configure<Settings>(options => configurationRoot.GetSection("settings").Bind(options));

            services.AddLogging(c =>
            {
                c.SetMinimumLevel(LogLevel.Trace);

                c.AddFilter(LogConfiguration.TestHost, LogLevel.Trace);
                c.AddFilter(LogConfiguration.TestRunner, LogLevel.Trace);

                c.AddFile(o =>
                {
                    o.RootPath = _outputDirectory;
                    o.FileAccessMode = LogFileAccessMode.KeepOpenAndAutoFlush;

                    o.Files = new[]
                    {
                        new LogFileOptions
                        {
                            Path = "testhost-" + DateTime.Now.ToString("yy-MM-dd-H-mm") + ".log",
                            MinLevel = new Dictionary<string, LogLevel>
                                { { LogConfiguration.TestHost, LogLevel.Trace } }
                        },
                        new LogFileOptions
                        {
                            Path = "testprocess-" + DateTime.Now.ToString("yy-MM-dd-H-mm") + ".log",
                            MinLevel = new Dictionary<string, LogLevel>
                                { { LogConfiguration.TestRunner, LogLevel.Trace } }
                        }
                    };
                });
            });

            services.AddSingleton<Program>();
            var serviceProvider = services.BuildServiceProvider();
            var program = serviceProvider.GetService<Program>();

            await program.Run(settings);
        }

        private static Settings ParseCommandlineArguments(string[] args)
        {
            var settings = new Settings();

            var result = Parser.Default.ParseArguments<Settings>(args)
                .WithParsed(o => { settings = o; });

            if (result.Tag == ParserResultType.NotParsed) Environment.Exit(1);

            return settings;
        }

        private async Task Run(Settings settings)
        {
            // Once the program exits, set the console color back to what it was initially,
            // otherwise your console might become cyan after running Faultify
            var initialColor = Console.ForegroundColor;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => { Console.ForegroundColor = initialColor; };

            ConsoleMessage.PrintLogo();
            ConsoleMessage.PrintSettings(settings);

            var progress = new Progress<MutationRunProgress>();
            progress.ProgressChanged += OnProgressChanged;

            var progressTracker = new MutationSessionProgressTracker(progress, _loggerFactory);

            if (!File.Exists(settings.TestProjectPath))
            {
                progressTracker.LogCriticalErrorAndExit($"Test project '{settings.TestProjectPath}' can not be found.");
            }

            var testResult = await RunMutationTest(settings, progressTracker);

            progressTracker.LogBeginReportBuilding(settings.ReportType, settings.ReportPath);
            await GenerateReport(testResult, settings);
            progressTracker.LogEndFaultify(settings.ReportPath);
            await Task.CompletedTask;
        }


        private static void OnProgressChanged(object sender, MutationRunProgress s)
        {
            ConsoleColor color = ConsoleColor.Cyan;
            bool startWithBlankLine = true;

            switch (s.LogMessageType)
            {
                case LogMessageType.TestRunUpdate:
                    // Remembers the location of the cursor the first time around.
                    // Next time a TestRunUpdate is printed, it will put the cursor
                    // back there and overwrite the previous message.
                    if (cursorPosition.Item1 == 0 && cursorPosition.Item2 == 0)
                        cursorPosition = (Console.CursorLeft, Console.CursorTop);

                    Console.SetCursorPosition(cursorPosition.Item1, cursorPosition.Item2);
                    break;
                case LogMessageType.Error:
                    color = ConsoleColor.Red;
                    break;
                case LogMessageType.Other:
                    startWithBlankLine = false;
                    break;
            }

            string message = $"{(startWithBlankLine ? "\n" : "")}> [{s.Progress}%] {s.Message}";
            ConsoleMessage.Print(message, color);
        }

        private async Task<TestProjectReportModel> RunMutationTest(Settings settings,
            MutationSessionProgressTracker progressTracker)
        {
            ITestHostRunFactory testHost = settings.TestHost switch
            {
                _ => new DotnetTestHostRunnerFactory() // TODO: Use Faultify.TestRunner.XUnit/NUnit for in memory testing. 
            };

            var mutationTestProject =
                new MutationTestProject(settings.TestProjectPath, settings.MutationLevel, settings.Parallel,
                    _loggerFactory, testHost, settings.TimeOut);

            return await mutationTestProject.Test(progressTracker, CancellationToken.None);
        }

        private async Task GenerateReport(TestProjectReportModel testResult, Settings settings)
        {
            if (string.IsNullOrEmpty(settings.ReportPath))
                settings.ReportPath = Directory.GetCurrentDirectory();

            var mprm = new MutationProjectReportModel();
            mprm.TestProjects.Add(testResult);

            var reporter = ReportFactory(settings.ReportType);
            var reportBytes = await reporter.CreateReportAsync(mprm);

            var reportFileName = DateTime.Now.ToString("yy-MM-dd-H-mm") + reporter.FileExtension;

            await File.WriteAllBytesAsync(Path.Combine(_outputDirectory, reportFileName), reportBytes);
        }

        private IReporter ReportFactory(string type)
        {
            return type?.ToUpper() switch
            {
                "PDF" => new PdfReporter(),
                "HTML" => new HtmlReporter(),
                _ => new JsonReporter()
            };
        }

        private static IConfigurationRoot BuildConfigurationRoot()
        {
            var builder = new ConfigurationBuilder();
            builder.AddUserSecrets<Program>(true);
            var configurationRoot = builder.Build();
            return configurationRoot;
        }
    }
}