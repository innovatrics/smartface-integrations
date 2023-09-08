using System;
using System.IO;
using System.Threading;
using System.Globalization;
using CsvHelper;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public class CsvUtil
    {
        private static  CultureInfo CULTURE = CultureInfo.InvariantCulture;

        public static void ExportResultsToCsv<T>(
            string filePath,
            T[] results,
            CancellationToken cancellationToken = default
        )
        {
            var totalCount = results.Length;
            Console.WriteLine($"Exporting {totalCount} rows.");

            using (var mem = new MemoryStream())
            {
                using (var writer = new StreamWriter(mem))
                {
                    using (var csvWriter = new CsvWriter(writer, CULTURE))
                    {

                        csvWriter.WriteHeader<T>();
                        csvWriter.NextRecord(); // adds new line after header

                        csvWriter.WriteRecords(results);

                        csvWriter.Flush();
                        writer.Flush();

                        Console.WriteLine($"Writing export to file {filePath}...");
                        cancellationToken.ThrowIfCancellationRequested();

                        File.WriteAllBytes(filePath, mem.ToArray());

                        Console.WriteLine($"Writing done.");
                    }
                }
            }
        }

        internal static string ToString(float? value)
        {
            return value?.ToString(CULTURE);
        }
    }
}