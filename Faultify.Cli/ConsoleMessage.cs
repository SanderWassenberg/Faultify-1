using System;

namespace Faultify.Cli
{
    internal static class ConsoleMessage
    {
        private static readonly object ConsoleMutex = new object();

        private static readonly string _logo = @"    
             ______            ____  _ ____     
            / ____/___ ___  __/ / /_(_) __/_  __
           / /_  / __ `/ / / / / __/ / /_/ / / / 
          / __/ / /_/ / /_/ / / /_/ / __/ /_/ / 
         / _/    \__,_/\__,_/_/\__/_/_/  \__,/ 
                                       /____/  
        ";

        public static void PrintLogo()
        {
            Print(_logo);
        }

        public static void PrintSettings(Settings settings)
        {
            var settingsString =
                "\n" +
                $"| Mutation Level: {settings.MutationLevel}\n" +
                $"| Test Host: {settings.TestHost}\n" +
                $"| Report Path: {settings.ReportPath}\n" +
                $"| Report Type: {settings.ReportType}\n" +
                $"| Test Project Path: {settings.TestProjectPath}\n" +
                "\n";

            Print(settingsString, ConsoleColor.Green);
        }

        public static void Print(string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            lock (ConsoleMutex)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
    }
}