// Add necessary packages using directives
// dotnet add package GraphQL.Client
// dotnet add package GraphQL.Client.Serializer.Newtonsoft
// dotnet add package Newtonsoft.Json
// dotnet add package System.Drawing.Common

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using System.Diagnostics;

// Initialize variables
string serverUrl = "http://<server-ip-or-hostname>/graphql/";
string cameraId = "<camera-guid>";

int cameraSize_width = 1920;
int cameraSize_height = 1080;
int takeCount = 1000;
int skipCount = 0;
int maxHeat = 0;
string bmpFilename = "CameraHeatmap.png";

Console.WriteLine("Camera Heatmap");

// Create a 2D array (heatmap) initialized to zero
int[,] heatmap = new int[cameraSize_width, cameraSize_height];

// Initialize the GraphQL client
var graphQLClient = new GraphQLHttpClient(serverUrl, new NewtonsoftJsonSerializer());

// Characters used for loading animation
char[] spinnerFrames = { '-', '\\', '|', '/' };
int currentFrame = 0;

// Using pagination to find all pedestrians on the camera
bool finishedQuery = false;
while (!finishedQuery)
{

    // Display the current spinner frame
    Console.Write(spinnerFrames[currentFrame]);
    // Reset cursor position to overwrite the spinner on the next iteration
    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
    // Cycle to the next frame
    currentFrame = (currentFrame + 1) % spinnerFrames.Length;


    // Define a GraphQL query
    // Will use all the pedestrian data from a camera
    var query = new GraphQLRequest
    {
        Query = $@"
        query {{
            pedestrians(take: {takeCount}, skip: {skipCount}, where: {{ streamId: {{ eq: ""{cameraId}"" }} }}) {{
                items {{
                    cropLeftTopX
                    cropLeftTopY
                    cropRightBottomX
                    cropRightBottomY
                }}
                pageInfo {{
                    hasNextPage
                }}
            }}
        }}"
    };

    // Send the query request and await the response
    var response = await graphQLClient.SendQueryAsync<dynamic>(query);

    // Extract the pedestrian data
    var pedestrians = response.Data?.pedestrians?.items as JArray;

    // Process pedestrian rectangles
    if (pedestrians != null)
    {
        foreach (var item in pedestrians)
        {
            // Ensure coordinates are within valid range
            int leftTopX = Math.Max(0, (int)item["cropLeftTopX"]);
            int leftTopY = Math.Max(0, (int)item["cropLeftTopY"]);
            int rightBottomX = Math.Min(cameraSize_width - 1, (int)item["cropRightBottomX"]);
            int rightBottomY = Math.Min(cameraSize_height - 1, (int)item["cropRightBottomY"]);

            for (int x = leftTopX; x <= rightBottomX; x++)
            {
                for (int y = leftTopY; y <= rightBottomY; y++)
                {
                    heatmap[x, y] += 1; // Increment heatmap intensity
                    maxHeat = Math.Max(maxHeat, heatmap[x, y]);
                }
            }
        }

        // Update the skip count based on the number of items
        skipCount += pedestrians.Count;
        // Check if there's more data to fetch
        finishedQuery = !(bool)response.Data.pedestrians.pageInfo.hasNextPage;
    }
    else
    {
        finishedQuery = true;
        Console.WriteLine("No data retrieved or query resulted in an error.");
    }
}

// Function to convert heat value to RGB color
Color HeatToColor(int value, int maxValue)
{
    if (maxValue == 0) return Color.Black;

    double percentage = (double)value / maxValue;

    // Linear interpolation between blue and red
    int r = (int)(255 * percentage);
    int g = 0;
    int b = (int)(255 * (1 - percentage));

    return Color.FromArgb(r, g, b);
}

// Create and save the heatmap as a BMP image
using (Bitmap bmp = new Bitmap(cameraSize_width, cameraSize_height))
{
    for (int x = 0; x < cameraSize_width; x++)
    {
        for (int y = 0; y < cameraSize_height; y++)
        {
            Color color = HeatToColor(heatmap[x, y], maxHeat);
            bmp.SetPixel(x, y, color);
        }
    }
    bmp.Save(bmpFilename,ImageFormat.Png);
}

Console.WriteLine($"Heatmap BMP image created as {bmpFilename}.");

// Use Process.Start to open the image
Process.Start(new ProcessStartInfo(bmpFilename) { UseShellExecute = true });
