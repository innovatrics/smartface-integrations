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
using ChangiDataExport.Export;
using ChangiDataExport.Models;
using ChangiDataExport.Csv;
using ChangiDataExport.Models.Odata;

namespace ChangiDataExport.Commands
{
    public class ExportIndividualsCommand : Command
    {
        private const int PageSize = 1000;

        public const string Command = "individuals";
        public const string CommandAlias = "i";
        public const string DescriptionText = "Exports Individuals into HTML or CSV.";

        public const string InstanceOption = "--instance";
        public const string InstanceOptionShort = "-i";

        public const string GroupingsOption = "--groupings";
        public const string GroupingsOptionShort = "-g";

        public const string ResultsDirectoryOption = "--results";
        public const string ResultsDirectoryOptionShort = "-r";

        public const string FormatOption = "--format";
        public const string FormatOptionShort = "-f";

        public const string MatchThresholdOption = "--match-threshold";
        public const string MatchThresholdOptionShort = "-m";

        public const string DateFromOption = "--date-from";
        public const string FromOptionShort = "-df";

        public const string DateToOption = "--date-to";
        public const string ToOptionShort = "-dt";

        public const string MinimumTrackletsOption = "--min-tracklets";
        public const string MinimumTrackletsOptionShort = "-mt";
        
        public const string MinimumStreamsOption = "--min-cameras";
        public const string MinimumStreamsOptionShort = "-mc";

        public ExportIndividualsCommand() : base(Command, DescriptionText)
        {
            AddAlias(CommandAlias);
            AddAlias("export");

            var instanceOption = new Option<string>(
                InstanceOption,
                "Specify the SmartFace instance"
            )
            {
                Required = false
            };

            instanceOption.AddAlias(InstanceOptionShort);

            var groupingsOption = new Option<int[]>(
                GroupingsOption,
                "Specify GroupingMetadataIds"
            )
            {
                Required = false
            };

            groupingsOption.AddAlias(GroupingsOptionShort);

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

            var minTrackletsOption = new Option<int?>(
                            MinimumTrackletsOption,
                            "Minimum Tracklets count to be included in results"
                        )
            {
                Required = false
            };

            minTrackletsOption.AddAlias(MinimumTrackletsOptionShort);

            var minStreamsOption = new Option<int?>(
                            MinimumStreamsOption,
                            "Minimum Tracklets count to be included in results"
                        )
            {
                Required = false
            };

            minStreamsOption.AddAlias(MinimumStreamsOptionShort);

            Add(instanceOption);
            Add(resultsDirectoryOption);
            Add(formatOption);
            Add(dateFromOption);
            Add(dateToOption);
            Add(minTrackletsOption);
            Add(minStreamsOption);

            Handler = CommandHandler.Create(new Func<CancellationToken, string, int[], string, ExportFormat, DateTime?, DateTime?, int?, int?, Task>(RunExportAsync));
        }

        private async Task RunExportAsync(
            CancellationToken cancellation,
            string instance,
            int[] groupings,
            string results,
            ExportFormat format,
            DateTime? dateFrom,
            DateTime? dateTo,
            int? minTracklets,
            int? minStreamsCount
        )
        {
            Console.WriteLine($"Running export of Individuals");

            var individuals = await fetchAllDataAsync(
                instance,
                groupings,
                dateFrom,
                dateTo,
                minTracklets,
                minStreamsCount,
                cancellation);

            results = results ?? "./Output/";

            var individualResults = await fetchIndividualDataAsync(instance, individuals, cancellation);

            switch (format)
            {
                case ExportFormat.Csv:

                    var csvFileName = $"individuals-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.csv";
                    var csvFilePath = Path.Combine(results, csvFileName);

                    ResultsCsvExporter.ExportResultsToCsv(csvFilePath, individualResults, cancellation);
                    break;
                case ExportFormat.Html:

                    var htmlFileName = $"individuals-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.html";
                    var htmlFilePath = Path.Combine(results, htmlFileName);

                    ResultsHtmlExporter.ExportResultsToHtml(htmlFilePath, individualResults, cancellation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private async Task<Individual[]> fetchAllDataAsync(
            string instanceUrl,
            int[] groupingMetadataIds,
            DateTime? dateFrom,
            DateTime? dateTo,
            int? minTrackletsCount,
            int? minStreamsCount,
            CancellationToken cancellationToken = default
        )
        {
            using var restClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8098/api/v1/") };
            using var odataClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8099/odata/") };

            Console.WriteLine("Fetching all Individuals");

            var individualsBatches = new List<List<Individual>>();
            var skip = 0;
            bool lastQueryNonEmpty;
            
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var queryUrlBuilder = generateIndividualsQueryUrl(groupingMetadataIds, dateFrom, dateTo, PageSize, skip);

                var newData =
                    (await odataClient.GetGenericAsync<ResultsListWrapper<Individual>>(queryUrlBuilder, cancellationToken))
                    .Value;

                lastQueryNonEmpty = newData?.Count > 0;

                if (lastQueryNonEmpty)
                {
                    individualsBatches.Add(newData);
                }

                Console.WriteLine($"{newData.Count} individuals fetched...");

                skip += PageSize;

            } while (lastQueryNonEmpty);

            Console.WriteLine($"{individualsBatches.Count} batches were fetched.");

            var individuals = individualsBatches.SelectMany(s => s).ToArray();

            if (minTrackletsCount > 0)
            {
                individuals = individuals.Where(w => w.Tracklets?.Count >= minTrackletsCount).ToArray();
            }

            if (minStreamsCount > 0)
            {
                individuals = individuals.Where(w => w.Tracklets?.Select(s => s.StreamId).Distinct().Count() >= minStreamsCount).ToArray();
            }

            return individuals;
        }

        private async Task<IndividualResult[]> fetchIndividualDataAsync(string instanceUrl, Individual[] individuals, CancellationToken cancellation)
        {
            using var restClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8098/api/v1/") };
            using var odataClient = new HttpClient { BaseAddress = new Uri($"{instanceUrl}:8099/odata/") };

            Console.WriteLine("Fetching individuals with data...");

            var individualResults = new List<IndividualResult>();

            var count = 0;

            foreach (var individual in individuals)
            {
                Console.WriteLine($"Fetching Individual {individual.Id}");

                count++;

                cancellation.ThrowIfCancellationRequested();

                var firstTrackletId = individual.Tracklets.OrderBy(o => o.TimeAppeared).Select(s => s.Id).FirstOrDefault();
                var lastTrackletId = individual.Tracklets.OrderBy(o => o.TimeDisappeared).Select(s => s.Id).LastOrDefault();

                var trackletIds = new Guid[] { firstTrackletId, lastTrackletId };

                var queryUrl = generateTrackletsQueryUrl(trackletIds.Distinct().ToArray());

                var tracklets =
                    (await odataClient.GetGenericAsync<ResultsListWrapper<Tracklet>>(queryUrl, cancellation))
                    .Value;

                Console.WriteLine($"{count}/{individuals.Length} Individuals updated...");

                var firstTracklet = tracklets.Where(w => w.Id == firstTrackletId).SingleOrDefault();
                var lastTracklet = tracklets.Where(w => w.Id == lastTrackletId).SingleOrDefault();

                Face firstFace, lastFace;

                if (firstTrackletId == lastTrackletId)
                {
                    firstFace = firstTracklet.Faces.OrderBy(o => o.CreatedAt).FirstOrDefault();
                    lastFace = lastTracklet.Faces.OrderBy(o => o.CreatedAt).LastOrDefault();
                }
                else
                {
                    firstFace = firstTracklet.Faces.OrderBy(o => o.TemplateQuality).LastOrDefault();
                    lastFace = lastTracklet.Faces.OrderBy(o => o.TemplateQuality).LastOrDefault();
                }

                var individualResult = IndividualResult.FromDbResult(individual);

                if (firstFace?.ImageDataId != null)
                {
                    individualResult.FirstFace = await this.downloadImageAsync(restClient, firstFace.ImageDataId);
                }

                if (lastFace?.ImageDataId != null
                // && firstFace.ImageDataId != lastFace.ImageDataId
                )
                {
                    individualResult.LastFace = await this.downloadImageAsync(restClient, lastFace.ImageDataId);
                }

                individualResult.EntranceCamera = firstTracklet.Stream?.Name;
                individualResult.ExitCamera = lastTracklet.Stream?.Name;

                individualResult.ExitTime = lastTracklet.TimeDisappeared;

                individualResults.Add(individualResult);
            }

            return individualResults
                        .OrderByDescending(o => o.EntranceTime)
                        .ToArray();
        }

        private async Task<byte[]> downloadImageAsync(HttpClient restClient, Guid imageDataId)
        {
            return await restClient.GetByteArrayAsync($"Images/{imageDataId}");
        }

        private static string generateIndividualsQueryUrl(
            int[] groupingMetadataIds,
            DateTime? dateFrom,
            DateTime? dateTo,
            int top,
            int skip
        )
        {
            var queryUrlBuilder = new StringBuilder("Individuals?");

            var queryFilter = $"$filter=";

            if (groupingMetadataIds?.Length > 0)
            {
                queryFilter += $"(GroupingMetadataId in [{string.Join(',', groupingMetadataIds)}]) and";
            }

            if (dateFrom != null)
            {
                queryFilter += $"(EntranceTime ge {dateFrom.Value.ToString("yyyy-MM-ddTHH:mm:ss")}Z) and";
            }

            if (dateTo != null)
            {
                queryFilter += $"(ExitTime le {dateTo.Value.ToString("yyyy-MM-ddTHH:mm:ss")}Z) and";
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

            queryUrlBuilder.Append($"$expand=BestFace($select=Id,TemplateQuality,ImageDataId),Tracklets($select=Id,TimeAppeared,TimeDisappeared,StreamId)&");
            queryUrlBuilder.Append($"$top={top}&$skip={skip}&");
            return queryUrlBuilder.ToString();
        }

        private static string generateTrackletsQueryUrl(Guid[] ids)
        {
            var queryUrlBuilder = new StringBuilder("Tracklets?");
            queryUrlBuilder.Append($"$filter=Id in [{string.Join(',', ids.Select(s => $"'{s}'"))}]&");
            queryUrlBuilder.Append($"$expand=Faces($select=Id,ImageDataId,TemplateQuality,StreamId),Stream($select=Id,Name)&");
            return queryUrlBuilder.ToString();
        }
    }
}