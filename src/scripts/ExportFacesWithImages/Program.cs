using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public class Program
    {
        private const string SMARTFACE_API_URL = "http://10.11.64.18:8098";
        private const string SMARTFACE_GRAPHQL_URL = "http://10.11.64.18:8097/graphql";


        static async Task<int> Main(string[] args)
        {
            var cameras = new string[] {
                "1b6d42bd-431a-4bc3-a115-4008bd726dbb",
                "19b10e5b-aebf-4c00-8ef1-24c2def86925",
                "847f65b6-3211-41ae-8a33-72d595a48ce7",
                "d5ff8f40-f900-4492-8ecc-6a2539648964",
                "f9de3d6b-8f98-4bef-13bb-08db0aa598ba",
                "6b7b8f5c-6c28-4ccb-1b9e-08db2952c2b7"
            }
                .Select(s => Guid.Parse(s))
                .ToArray()
            ;

            var faces = await GraphQlUtil.GetAllFacesWithMatches(SMARTFACE_GRAPHQL_URL, cameras);

            var facesGrouped = faces.Where(w => w.tracklet != null)
                                    .GroupBy(g => g.tracklet.Id)
                                    .Where(w => w.Count() > 1);

            foreach(var group in facesGrouped)
            {
                Console.WriteLine($"Tracklet {group.Key} has {group.Count()} faces");
            }

            var targetDirPath = Path.Combine("./Output/", $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}");

            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }

            var csvFilePath = Path.Combine(targetDirPath, $"faces.csv");

            CsvUtil.ExportResultsToCsv(
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
                        FULLFRAME = s.Frame?.ImageDataId,

                        BOUNDING_BOX_COORINATES_LT = $"[{CsvUtil.ToString(s.CropLeftTopX)},{CsvUtil.ToString(s.CropLeftTopY)}]",
                        BOUNDING_BOX_COORINATES_RT = $"[{CsvUtil.ToString(s.CropRightTopX)},{CsvUtil.ToString(s.CropRightTopY)}]",
                        BOUNDING_BOX_COORINATES_LB = $"[{CsvUtil.ToString(s.CropLeftBottomX)},{CsvUtil.ToString(s.CropLeftBottomY)}]",
                        BOUNDING_BOX_COORINATES_RB = $"[{CsvUtil.ToString(s.CropRightBottomX)},{CsvUtil.ToString(s.CropRightBottomY)}]",
                    })
                    .ToArray()
            );

            var targetCropsDirPath = Path.Combine(targetDirPath, "crops");

            if (!Directory.Exists(targetCropsDirPath))
            {
                Directory.CreateDirectory(targetCropsDirPath);
            }

            var targetFramesDirPath = Path.Combine(targetDirPath, "full_frames");

            if (!Directory.Exists(targetFramesDirPath))
            {
                Directory.CreateDirectory(targetFramesDirPath);
            }

            foreach (var face in faces)
            {
                Console.WriteLine($"Downloading crop {face.ImageDataId}");

                var imageCrop = await ApiUtil.GetImageAsync(SMARTFACE_API_URL, face.ImageDataId);
                await File.WriteAllBytesAsync(Path.Combine(targetCropsDirPath, $"{face.ImageDataId}.jpeg"), imageCrop);

                if (face.Frame?.ImageDataId != null)
                {
                    var targetFramePath = Path.Combine(targetFramesDirPath, $"{face.Frame?.ImageDataId}.jpeg");

                    if (!File.Exists(targetFramePath))
                    {
                        Console.WriteLine($"Downloading crop {face.Frame?.ImageDataId}");

                        var imageFrame = await ApiUtil.GetImageAsync(SMARTFACE_API_URL, face.Frame.ImageDataId.Value);
                        await File.WriteAllBytesAsync(targetFramePath, imageFrame);
                    }
                }
            }

            Console.WriteLine($"Done, quit");

            return 0;
        }
    }
}
