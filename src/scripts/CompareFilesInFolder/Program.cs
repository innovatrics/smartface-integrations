using System.Globalization;
using System.Net.Http.Json;

using CsvHelper;
using Newtonsoft.Json;

namespace SmartFace.Integrations.CompareFilesInFolder
{
    public class Program
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task Main()
        {
            string rootFolderPath = @"C:\Users\user\Downloads\source";

            List<FileResult> results = new List<FileResult>();

            // Recursively browse folders
            await ProcessDirectory(rootFolderPath, results);

            // Write results to CSV
            WriteResultsToCsv(results, @"C:\Users\user\Downloads\results.csv");

            Console.WriteLine("Process completed. Results saved to results.csv.");
        }

        private static async Task ProcessDirectory(string folderPath, List<FileResult> results)
        {
            // Get all files that match *.jpg and *.jpg.tmpl in the current directory
            var jpgFiles = Directory.GetFiles(folderPath, "*.jpg", SearchOption.AllDirectories);

            var uniqueFiles = jpgFiles.Select(fullPath =>
            {
                string directory = Path.GetDirectoryName(fullPath);
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

                return Path.Combine(directory, filenameWithoutExtension);
            })
            .Distinct().ToArray();

            // Process each *.jpg file
            foreach (var filePath in uniqueFiles)
            {
                await ProcessFile(filePath, results);
            }
        }

        private static async Task ProcessFile(string filePath, List<FileResult> results)
        {
            var fileResult = new FileResult
            {
                FileName = Path.GetFileNameWithoutExtension(filePath)
            };

            if (File.Exists(filePath + ".jpg"))
            {
                try
                {
                    var result1 = await SearchAsync(filePath + ".jpg");

                    fileResult.ImageSearchScore = result1?.Score;
                }
                catch (Exception ex)
                {
                    fileResult.ImageSearchScore = -1;
                }
            }

            if (File.Exists(filePath + ".jpg.tmpl"))
            {
                try
                {
                    var result2 = await SearchByTemplateAsync(filePath + ".jpg.tmpl");

                    fileResult.TemplateSearchScore = result2?.Score;
                }
                catch (Exception ex)
                {
                    fileResult.TemplateSearchScore = -1;
                }
            }

            results.Add(fileResult);
        }


        private static async Task<MatchResult> SearchAsync(string fileName)
        {
            var imageBytes = File.ReadAllBytes(fileName);

            var requestData = new
            {
                image = new
                {
                    data = Convert.ToBase64String(imageBytes)
                },
                watchlistId = new[] { "6c45a650-fe9c-4056-8b0b-05d9a749d234" },
                threshold = 40,
                maxResultCount = 1,
                faceValidationMode = "none",
                faceDetectorConfig = new
                {
                    minFaceSize = 15,
                    maxFaceSize = 600,
                    maxFaces = 20,
                    confidenceThreshold = 400
                },
                faceDetectorResourceId = "cpu",
                templateGeneratorResourceId = "cpu"
            };

            var response = await client.PostAsJsonAsync("http://10.11.83.35:8098/api/v1/Watchlists/Search", requestData);


            var jsonResponse = await response.Content.ReadAsStringAsync();

            Console.WriteLine(jsonResponse);

            response.EnsureSuccessStatusCode();

            var parsedResponse = JsonConvert.DeserializeObject<List<WatchlistResponse>>(jsonResponse);

            if (parsedResponse != null && parsedResponse.Count > 0)
            {
                var result = parsedResponse[0];
                var matchResult = result.MatchResults?.FirstOrDefault();
                // $"Score: {matchResult?.Score}, Name: {matchResult?.DisplayName}, Gender: {result.Gender}, Age: {result.Age}";
                return matchResult;
            }

            return null;
        }

        private static async Task<MatchResult> SearchByTemplateAsync(string fileName)
        {
            var templateBytes = File.ReadAllBytes(fileName);

            var requestData = new
            {
                template = Convert.ToBase64String(templateBytes),
                watchlistId = new[] { "6c45a650-fe9c-4056-8b0b-05d9a749d234" },
                threshold = 40,
                maxResultCount = 1
            };

            var response = await client.PostAsJsonAsync("http://10.11.83.35:8098/api/v1/Watchlists/SearchByTemplate", requestData);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            Console.WriteLine(jsonResponse);

            response.EnsureSuccessStatusCode();

            var parsedResponse = JsonConvert.DeserializeObject<WatchlistResponse>(jsonResponse);

            if (parsedResponse != null)
            {
                var matchResult = parsedResponse.MatchResults?.FirstOrDefault();
                // $"Score: {matchResult?.Score}, Name: {matchResult?.DisplayName}, Gender: {result.Gender}, Age: {result.Age}";
                return matchResult;
            }

            return null;
        }

        private static void WriteResultsToCsv(List<FileResult> results, string csvFilePath)
        {
            using (var writer = new StreamWriter(csvFilePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(results);
            }
        }
    }

    public class FileResult
    {
        public string FileName { get; set; }
        public double? ImageSearchScore { get; set; }
        public double? TemplateSearchScore { get; set; }
    }

    public class WatchlistResponse
    {
        public ICollection<MatchResult> MatchResults { get; set; }
        public SpoofCheckResult SpoofCheckResult { get; set; }
        public double? CropLeftTopX { get; set; }
        public double? CropLeftTopY { get; set; }
        public double? CropRightTopX { get; set; }
        public double? CropRightTopY { get; set; }
        public double? CropLeftBottomX { get; set; }
        public double? CropLeftBottomY { get; set; }
        public double? CropRightBottomX { get; set; }
        public double? CropRightBottomY { get; set; }
        public double? Quality { get; set; }
        public double? LeftEyeX { get; set; }
        public double? LeftEyeY { get; set; }
        public double? RightEyeX { get; set; }
        public double? RightEyeY { get; set; }
        public double? Age { get; set; }
        public string Gender { get; set; }
        public double? FaceSize { get; set; }
        public double? FaceMaskConfidence { get; set; }
        public double? NoseTipConfidence { get; set; }
        public string FaceMaskStatus { get; set; }
        public double? Sharpness { get; set; }
        public double? Brightness { get; set; }
        public double? TintedGlasses { get; set; }
        public double? HeavyFrame { get; set; }
        public double? GlassStatus { get; set; }
        public double? PitchAngle { get; set; }
        public double? YawAngle { get; set; }
        public double? RollAngle { get; set; }
        public double? FaceArea { get; set; }
        public double? TemplateQuality { get; set; }
        public double? FaceOrder { get; set; }
    }

    public class MatchResult
    {
        public double? Score { get; set; }
        public string WatchlistMemberId { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        public string WatchlistDisplayName { get; set; }
        public string WatchlistFullName { get; set; }
        public string WatchlistId { get; set; }
        public string PreviewColor { get; set; }
    }

    public class SpoofCheckResult
    {
        public bool Performed { get; set; }
        public bool Passed { get; set; }
        public LivenessSpoofCheck DistantLivenessSpoofCheck { get; set; }
        public LivenessSpoofCheck NearbyLivenessSpoofCheck { get; set; }
    }

    public class LivenessSpoofCheck
    {
        public bool Performed { get; set; }
        public bool Passed { get; set; }
        public double? Score { get; set; }
        public List<NotPerformedReason> NotPerformedReasons { get; set; }
    }

    public class NotPerformedReason
    {
        public string ReasonMessage { get; set; }
    }
}