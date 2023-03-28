using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using CsvHelper;
using Innovatrics.SmartFace.Integrations.DataExportTool.Export;
using Innovatrics.SmartFace.Integrations.DataExportTool.Models;
using Innovatrics.SmartFace.Integrations.DataExportTool.Csv;
using Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Commands
{
    public class ExportFacesCommand : Command
    {
        private const int PageSize = 1000;

        public const string Command = "faces";
        public const string CommandAlias = "f";
        public const string DescriptionText = "Exports Faces info HTML or CSV.";

        public const string InstanceOption = "--instance";
        public const string InstanceOptionShort = "-i";

        public const string CamerasOption = "--cameras";
        public const string CamerasOptionShort = "-c";

        public const string ResultsDirectoryOption = "--results";
        public const string ResultsDirectoryOptionShort = "-r";

        public const string FormatOption = "--format";
        public const string FormatOptionShort = "-f";

        public const string DateFromOption = "--date-from";
        public const string FromOptionShort = "-df";

        public const string DateToOption = "--date-to";
        public const string ToOptionShort = "-dt";

        public const string MinimumTrackletsOption = "--min-tracklets";
        public const string MinimumTrackletsOptionShort = "-min";

        public ExportFacesCommand() : base(Command, DescriptionText)
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

            var camerasOption = new Option<Guid[]>(
                CamerasOption,
                "Specify CameraIds"
            )
            {
                Required = false
            };

            camerasOption.AddAlias(CamerasOptionShort);

            var resultsDirectoryOption = new Option<string>(
                ResultsDirectoryOption,
                "Specify the directory, where results CSV will be found."
            )
            {
                Required = false
            };

            resultsDirectoryOption.AddAlias(ResultsDirectoryOptionShort);

            var formatOption = new Option<ExportFormat>(
                FormatOption,
                () => ExportFormat.Html,
                "Specify the format of the output."
            );

            formatOption.AddAlias(FormatOptionShort);

            var dateFromOption = new Option<DateTime?>(
                DateFromOption,
                "Take Invividials from Date & Time"
            )
            {
                Required = false
            };

            dateFromOption.AddAlias(FromOptionShort);

            var dateToOption = new Option<DateTime?>(
                            DateToOption,
                            "Take Invividials to Date & Time"
                        )
            {
                Required = false
            };

            dateToOption.AddAlias(ToOptionShort);

            Add(instanceOption);
            Add(resultsDirectoryOption);
            Add(formatOption);
            Add(dateFromOption);
            Add(dateToOption);

            Handler = CommandHandler.Create(new Func<CancellationToken, string, Guid[], string, ExportFormat, DateTime?, DateTime?, Task>(RunExportAsync));
        }

        private async Task RunExportAsync(
            CancellationToken cancellation,
            string instance,
            Guid[] cameras,
            string results,
            ExportFormat format,
            DateTime? dateFrom,
            DateTime? dateTo
        )
        {
            Console.WriteLine($"Running export of Faces");

            var faces = await fetchAllDataAsync(
                instance,
                cameras,
                dateFrom,
                dateTo,
                cancellation);

            results = results ?? "./Output/";

            var faceResults = await fetchFaceDataAsync(instance, faces, cancellation);

            switch (format)
            {
                case ExportFormat.Csv:
                    var csvFileName = $"faces-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.csv";
                    var csvFilePath = Path.Combine(results, csvFileName);
                    ResultsCsvExporter.ExportResultsToCsv(csvFilePath, faceResults, cancellation);
                    break;
                case ExportFormat.Html:
                    var htmlFileName = $"faces-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.html";
                    var htmlFilePath = Path.Combine(results, htmlFileName);
                    ResultsHtmlExporter.ExportResultsToHtml(htmlFilePath, faceResults, cancellation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private async Task<Face[]> fetchAllDataAsync(
            string instanceUrl,
            Guid[] cameraIds,
            DateTime? dateFrom,
            DateTime? dateTo,
            CancellationToken cancellationToken
        )
        {
            using var restClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8098/api/v1/") };
            using var odataClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8099/odata/") };

            Console.WriteLine("Fetching all Faces");

            var faces = new List<List<Face>>();
            var skip = 0;
            bool lastQueryNonEmpty;
            
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var queryUrlBuilder = generateFacesQueryUrl(cameraIds, dateFrom, dateTo, PageSize, skip);

                var newData =
                    (await odataClient.GetGenericAsync<ResultsListWrapper<Face>>(queryUrlBuilder, cancellationToken))
                    .Value;

                lastQueryNonEmpty = newData?.Count > 0;

                if (lastQueryNonEmpty)
                {
                    faces.Add(newData);
                }

                Console.WriteLine($"{newData.Count} faces fetched...");

                skip += PageSize;

            } while (lastQueryNonEmpty);

            Console.WriteLine($"{faces.Count} batches were fetched.");

            var facesAll = faces.SelectMany(s => s).ToArray();

            return facesAll;
        }

        private async Task<FaceResult[]> fetchFaceDataAsync(string instanceUrl, Face[] faces, CancellationToken cancellation)
        {
            using var restClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8098/api/v1/") };
            using var odataClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8099/odata/") };

            Console.WriteLine("Fetching faces with data...");

            var faceResults = new List<FaceResult>();

            var count = 0;

            foreach (var face in faces)
            {
                Console.WriteLine($"Fetching Face {face.Id}");

                count++;

                cancellation.ThrowIfCancellationRequested();

                //         var firstTrackletId = face.Tracklets.OrderBy(o => o.TimeAppeared).Select(s => s.Id).FirstOrDefault();
                //         var lastTrackletId = face.Tracklets.OrderBy(o => o.TimeDisappeared).Select(s => s.Id).LastOrDefault();

                //         var trackletIds = new Guid[] { firstTrackletId, lastTrackletId };

                //         var queryUrl = generateTrackletsQueryUrl(trackletIds.Where(w => w != null).Distinct().ToArray());

                //         var tracklets =
                //             (await odataClient.GetGenericAsync<ResultsListWrapper<Tracklet>>(queryUrl, cancellation))
                //             .Value;

                //         Console.WriteLine($"{count}/{faces.Length} faces updated...");

                //         var firstTracklet = tracklets.Where(w => w.Id == firstTrackletId).SingleOrDefault();
                //         var lastTracklet = tracklets.Where(w => w.Id == lastTrackletId).SingleOrDefault();

                //         var firstFace = firstTracklet.Faces.OrderBy(o => o.TemplateQuality).LastOrDefault();
                //         var lastFace = lastTracklet.Faces.OrderBy(o => o.TemplateQuality).LastOrDefault();

                var faceResult = FaceResult.FromDbResult(face);

                faceResult.Image = await this.downloadImageAsync(restClient, face.ImageDataId);

                //         if (firstFace != null)
                //         {
                //             faceResult.FirstFace = await this.downloadImageAsync(restClient, firstFace.ImageDataId);
                //         }

                //         if (lastFace?.ImageDataId != null
                //         // && firstFace.ImageDataId != lastFace.ImageDataId
                //         )
                //         {
                //             faceResult.LastFace = await this.downloadImageAsync(restClient, lastFace.ImageDataId);
                //         }

                //         faceResult.EntranceCamera = firstTracklet.Stream?.Name;
                //         faceResult.ExitCamera = lastTracklet.Stream?.Name;

                //         faceResult.ExitTime = lastTracklet.TimeDisappeared;

                Console.WriteLine($"{count}/{faces.Length} Faces updated...");

                faceResults.Add(faceResult);
            }

            return faceResults.ToArray();
        }

        private async Task<byte[]> downloadImageAsync(HttpClient restClient, Guid imageDataId)
        {
            return await restClient.GetByteArrayAsync($"Images/{imageDataId}");
        }

        private static string generateFacesQueryUrl(
            Guid[] cameraIds,
            DateTime? dateFrom,
            DateTime? dateTo,
            int top,
            int skip
        )
        {
            var queryUrlBuilder = new StringBuilder("Faces?");

            var queryFilter = $"$filter=";

            if (cameraIds?.Length > 0)
            {
                queryFilter += $"(StreamId in [{string.Join(',', cameraIds)}]) and";
            }

            if (dateFrom != null)
            {
                queryFilter += $"(CreatedAt ge {dateFrom.Value.ToString("yyyy-MM-ddTHH:mm:ss")}Z) and";
            }

            if (dateTo != null)
            {
                queryFilter += $"(CreatedAt le {dateTo.Value.ToString("yyyy-MM-ddTHH:mm:ss")}Z) and";
            }

            if (queryFilter.EndsWith(" and"))
            {
                queryFilter = queryFilter.Substring(0, queryFilter.Length - (" and").Length);
            }

            if (queryFilter != $"$filter=")
            {
                queryUrlBuilder.Append(queryFilter);
                queryUrlBuilder.Append($"&");
            }

            queryUrlBuilder.Append($"$expand=Stream($select=Id,Name)&");
            queryUrlBuilder.Append($"$top={top}&$skip={skip}&");
            return queryUrlBuilder.ToString();
        }
    }
}