using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Text;

using Newtonsoft.Json;

using Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Export;
using Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Models;

namespace Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Commands
{
    public class FolderIdentificationCommand : Command
    {
        public const string Command = "folder";
        public const string CommandAlias = "i";
        public const string DescriptionText = "Takes all photos in a folder and search each of it against a watchlist";

        public const string InstanceOption = "--instance";
        public const string InstanceOptionShort = "-i";

        public const string SourceDirectoryOption = "--source";
        public const string SourceDirectoryOptionShort = "-s";

        public FolderIdentificationCommand() : base(Command, DescriptionText)
        {
            AddAlias(CommandAlias);

            var instanceOption = new Option<string>(
                InstanceOption,
                "Specify the SmartFace instance"
            )
            {
                Required = false
            };

            instanceOption.AddAlias(InstanceOptionShort);

            var sourceDirectoryOption = new Option<string>(
                SourceDirectoryOption,
                "Specify the directory, where results CSV will be found."
            )
            {
                Required = false
            };

            sourceDirectoryOption.AddAlias(SourceDirectoryOptionShort);

            Add(instanceOption);
            Add(sourceDirectoryOption);

            Handler = CommandHandler.Create(new Func<CancellationToken, string, string, Task>(RunAsync));
        }

        private async Task RunAsync(
            CancellationToken cancellation,
            string instance,
            string source
        )
        {
            Console.WriteLine($"Running {nameof(FolderIdentificationCommand)}");

            var files = readAllFiles(source);

            Console.WriteLine($"{(files?.Length) ?? 0} files found");

            if ((files?.Length ?? 0) == 0)
            {
                return;
            }

            var results = await searchAllFilesAsync(instance, files, cancellation);

            var htmlFileName = $"result-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.html";
            var htmlFilePath = Path.Combine(source, htmlFileName);

            ResultsHtmlExporter.ExportResultsToHtml(htmlFilePath, results, cancellation);
        }

        private static string[] readAllFiles(string source)
        {
            var files = new List<string>();

            if (source == null)
            {
                return new string[] { };
            }

            var sources = source.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine($"Found {(sources?.Length) ?? 0} sources");

            foreach (var directory in sources)
            {
                var filesInSource = Directory.GetFiles(directory, "*.*");

                files.AddRange(filesInSource);
            }

            return files.ToArray();
        }


        private async Task<SearchResult[]> searchAllFilesAsync(
            string instance,
            string[] files,
            CancellationToken cancellationToken = default
        )
        {
            var results = new List<SearchResult>();

            foreach (var file in files)
            {
                var r = await sendSearchRequestAsync(instance, file);
            }

            return results.ToArray();
        }

        private async static Task<SearchResponse> sendSearchRequestAsync(string instance, string imageFile)
        {
            var imageData = await File.ReadAllBytesAsync(imageFile);

            var requestPayload = new
            {
                image = new
                {
                    data = imageData
                },

                watchlistIds = new string[] { },

                threshold = 40,
                maxResultCount = 10,

                faceDetectorConfig = new
                {
                    minFaceSize = 25,
                    maxFaceSize = 900,
                    maxFaces = 20,
                    confidenceThreshold = 450
                },
            };

            var httpClient = new HttpClient();

            using (var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{instance}/api/v1/Watchlists/Search"
            ))
            {
                request.Content = new StringContent(
                    JsonConvert.SerializeObject(requestPayload),
                    Encoding.UTF8,
                    "application/json"
                );

                var stopwatch = Stopwatch.StartNew();

                Console.WriteLine($"Sending {imageFile}");

                var response = await httpClient.SendAsync(request);

                stopwatch.Stop();

                Console.WriteLine($"Sent in {stopwatch.ElapsedMilliseconds}ms, response {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<SearchResponse>(responseString);
                }

                return null;
            };
        }
    }
}