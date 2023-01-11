using System;
using System.Threading.Tasks;
using Grpc.Core;
using Innovatrics.Smartface;
using Innovatrics.SmartFace.Integrations.GrpcCamera.Services;

namespace Innovatrics.SmartFace.Integrations.GrpcCamera
{
    class Program
    {
        const int PORT = 30051;

        public static void Main(string[] args)
        {
            var server = new Server
            {
                Services = { VideoAnalyticService.BindService(new VideoAnalyticServiceImpl()) },
                Ports = { 
                    new ServerPort("0.0.0.0", PORT, ServerCredentials.Insecure),
                }
            };
            server.Start();

            Console.WriteLine($"{nameof(VideoAnalyticService)} server listening on port {PORT}");
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}