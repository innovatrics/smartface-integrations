using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.DataExportTool.Commands;

namespace Innovatrics.SmartFace.Integrations.DataExportTool
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var e = (Exception) eventArgs.ExceptionObject;
                Console.WriteLine($"Unhandled exception {e.Message}.");
            };
            var stopwatch = new Stopwatch();
            var cmd = new CommandBuilder();

            stopwatch.Start();
            var resultCode = await cmd.BuildCommand().InvokeAsync(args, new SystemConsole());
            stopwatch.Stop();

            Console.WriteLine($"Finished execution in {stopwatch.Elapsed}.");
            return resultCode;
        }
    }
}
