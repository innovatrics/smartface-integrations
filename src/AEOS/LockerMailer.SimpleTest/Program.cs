using System;
using System.Threading.Tasks;
using Innovatrics.SmartFace.Integrations.LockerMailer;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.SimpleTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await SimpleKeilaTest.TestKeilaConnection();
        }
    }
}

