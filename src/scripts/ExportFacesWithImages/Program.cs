using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public class Program
    {
        private const string SMARTFACE_GRAPHQL_URL = "http://srv-ba-6d:8097/graphql";

        static async Task<int> Main(string[] args)
        {
            var faces = await GraphQLUtil.GetAllFacesWithMatches(SMARTFACE_GRAPHQL_URL);

            var csvFilePath = Path.Combine("./Output/", $"faces-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.csv");
            ResultsCsvExporter.ExportResultsToCsv(
                csvFilePath,
                faces
                    .Select(s => new
                    {
                        ID = s.Id,
                        TRACKLET_ID = s.tracklet?.Id,
                        IDENTITY_ID = s.matchResults?.FirstOrDefault()?.watchlistMemberId,
                        IDENTITY_NAME = s.matchResults?.FirstOrDefault()?.watchlistMemberFullName,
                        TIMESTAMP = s.CreatedAt?.ToString("o"),

                        CROP = s.ImageDataId,
                        FRAME = s.Frame?.ImageDataId

                    })
                    .ToArray()
            );

            Console.WriteLine($"Press any key to quit");
            Console.ReadKey();

            return 0;
        }
    }
}
