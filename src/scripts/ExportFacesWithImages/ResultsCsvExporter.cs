using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;

namespace Innovatrics.SmartFace.Integrations.ExportFacesWithImages
{
    public class ResultsCsvExporter
    {
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
                    using (var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {

                        csvWriter.WriteHeader<T>();
                        csvWriter.NextRecord(); // adds new line after header

                        // foreach (var result in results)
                        // {
                        //     csvWriter.WriteRecord(result);
                        //     csvWriter.NextRecord();
                        // }

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
    }
}