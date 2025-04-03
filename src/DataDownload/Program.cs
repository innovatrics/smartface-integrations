using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

using Serilog;
using Minio;
using Minio.DataModel.Args;

namespace Innovatrics.SmartFace.DataDownload
{
    public class Program
    {
        private static IConfiguration _configuration;
        private static HttpClient _httpClient;
        private static IMinioClient _minioClient;

        private static async Task Main(string[] args)
        {
            _configuration = ConfigureBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration)
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Starting up");

            _httpClient = new HttpClient();

            _minioClient = CreateMinioClient();

            var palms = await GetPalmsAllAsync();

            await UploadPalmsAsync(palms);

            Log.Information("Program exited successfully");
            Log.CloseAndFlush();
        }

        private static IConfigurationRoot ConfigureBuilder(string[] args)
        {
            return new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json", optional: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();
        }

        private static async Task<List<GenericObject>> GetPalmsAllAsync()
        {
            var palms = new List<GenericObject>();

            var skip = 0;

            PalmsResponse palmPage;

            do
            {
                palmPage = await GetPalmsAsync(skip, 1000);
                palms.AddRange(palmPage.GenericObjects.Items);

                Log.Information("Fetched {Count} palms", palmPage.GenericObjects.Items.Length);

                skip += 1000;
            } while (palmPage.GenericObjects.PageInfo.HasNextPage);

            return palms;
        }

        private static async Task<PalmsResponse> GetPalmsAsync(int skipValue, int smartFaceSetPageSize)
        {
            var graphQlClient = CreateGraphQlClient();

            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query palms($skip: Int, $take: Int, $from: DateTime, $to: DateTime) {
                    genericObjects(
                        skip: $skip
                        take: $take
                        where: { processedAt: { gte: $from, lte: $to }, objectType: { eq: 2 } }
                        order: { processedAt: ASC }
                    ) {
                        items {
                            id
                            createdAt
                            processedAt
                            streamId
                            objectType
                            quality
                            size
                            frame {
                                id
                                imageDataId
                            }
                            imageDataId
                        }
                        
                        totalCount

                        pageInfo {
                            hasNextPage
                        }
                    }
                }",

                Variables = new
                {
                    skip = skipValue,
                    take = smartFaceSetPageSize,
                    from = new DateTime(2025, 03, 25, 0, 0, 0, DateTimeKind.Utc),
                    to = new DateTime(2025, 03, 25, 23, 59, 59, DateTimeKind.Utc)
                }
            };

            var response = await graphQlClient.SendQueryAsync<PalmsResponse>(graphQLRequest);

            if (response.Errors != null)
            {
                foreach (var error in response.Errors)
                {
                    Log.Error("Error: {Error}", error);
                }

                throw new Exception("Error fetching palms");
            }

            return response.Data;
        }

        private static GraphQLHttpClient CreateGraphQlClient()
        {
            var schema = _configuration.GetValue<string>("Source:GraphQL:Schema", "http");
            var host = _configuration.GetValue<string>("Source:GraphQL:Host", "SFGraphQL");
            var port = _configuration.GetValue<int>("Source:GraphQL:Port", 8097);
            var path = _configuration.GetValue<string>("Source:GraphQL:Path");

            var graphQlHttpClientOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri($"{schema}://{host}:{port}/{path}")
            };

            Log.Information("Subscription EndPoint {Endpoint}", graphQlHttpClientOptions.EndPoint);

            var client = new GraphQLHttpClient(graphQlHttpClientOptions, new NewtonsoftJsonSerializer());

            return client;
        }

        private static async Task UploadPalmsAsync(List<GenericObject> palms)
        {
            Log.Information("Uploading {Count} palms", palms.Count);

            foreach (var palm in palms)
            {
                Log.Information("Uploading palm {Id}", palm.Id);

                var palmFileName = $"{palm.StreamId}/{palm.ProcessedAt:yyyy-MM-dd}/{palm.ProcessedAt:HH}/{palm.ProcessedAt:HH-mm-ss}--{palm.Quality}-metadata.json";
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(palm);

                await UploadToMinioAsync("application/json", Encoding.UTF8.GetBytes(json), palmFileName);

                if (palm.Frame.ImageDataId != null)
                {
                    var frameImageData = await GetImageDataAsync(palm.Frame.ImageDataId.Value);

                    Log.Information("Frame image data length: {Length}", frameImageData.Length);

                    await UploadToMinioAsync("image/jpeg", frameImageData, $"{palm.StreamId}/{palm.ProcessedAt:yyyy-MM-dd}/{palm.ProcessedAt:HH}/{palm.ProcessedAt:HH-mm-ss}--{palm.Quality}-full-frame.jpg");
                }

                if (palm.ImageDataId != null)
                {
                    var palmImageData = await GetImageDataAsync(palm.ImageDataId.Value);

                    Log.Information("Palm image data length: {Length}", palmImageData.Length);

                    await UploadToMinioAsync("image/jpeg", palmImageData, $"{palm.StreamId}/{palm.ProcessedAt:yyyy-MM-dd}/{palm.ProcessedAt:HH}/{palm.ProcessedAt:HH-mm-ss}--{palm.Quality}-palm.jpg");
                }
            }
        }

        private static async Task<byte[]> GetImageDataAsync(Guid imageDataId)
        {
            var schema = _configuration.GetValue<string>("Source:API:Schema", "http");
            var host = _configuration.GetValue<string>("Source:API:Host", "SFAPI");
            var port = _configuration.GetValue<int>("Source:API:Port", 8098);

            var url = $"{schema}://{host}:{port}/api/v1/images/{imageDataId}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download image {imageDataId}. Status code: {response.StatusCode}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private static async Task UploadToMinioAsync(string contentType, byte[] imageData, string fileName)
        {
            var bucketName = _configuration.GetValue<string>("Minio:BucketName");
            var targetFolder = _configuration.GetValue<string>("Minio:TargetFolder");

            var objectName = $"{targetFolder}/{fileName}";

            var putObjectArgs = new PutObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(objectName)
                            .WithStreamData(new MemoryStream(imageData))
                            .WithObjectSize(imageData.Length)
                            .WithContentType(contentType);

            Log.Information("Uploading object: {objectName}, Size: {size} bytes", objectName, imageData.Length);

            // Upload the file
            var putObjectResponse = await _minioClient.PutObjectAsync(putObjectArgs);

            Log.Information("Upload finished with status {status} for {objectName}", putObjectResponse?.ResponseStatusCode, putObjectResponse?.ObjectName);
        }

        private static IMinioClient CreateMinioClient()
        {
            var endpoint = _configuration.GetValue<string>("Minio:Endpoint");
            var port = _configuration.GetValue<int>("Minio:Port", 9000);
            var accessKey = _configuration.GetValue<string>("Minio:AccessKey");
            var secretKey = _configuration.GetValue<string>("Minio:SecretKey");
            var useSsl = _configuration.GetValue<bool>("Minio:UseSsl", false);

            var minioClient = new MinioClient()
                                    .WithEndpoint(endpoint, port)
                                    .WithCredentials(accessKey, secretKey)
                                    .WithSSL(useSsl)
                                    .Build();

            return minioClient;
        }
    }
}
