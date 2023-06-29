using System.Text;

namespace Innovatrics.SmartFace.Integrations.IdentificationFromFolder.Export
{
    public class ResultsHtmlExporter
    {
        private const string DocumentOpening = @"
<!DOCTYPE html>
<html>
    <body>
        <style>
            td {
                // width: 200px;
                // height: 150px;
                text-align: center;
                vertical-align: middle;
                border: 1px solid black;
                white-space: nowrap;
                padding: 0 30px;
            }
            tr {
                // height: 150px;
            }
            img {
                max-width: 100px;
                max-height: 100px;
                width: auto;
            }
        </style>
        <table>
";

        private const string DocumentClosing = @"
        </table>
    </body>
</html>
";

        private const string HeaderColumnTemplate = @"
            <th width=""150"">
                {0}
            </th>
";
        private const string TextCellTemplate = @"
            <td>
                {0}
            </td>
";
        private const string GreenTextCellTemplate = @"
            <td bgcolor='#D0F0C0'>
                {0}
            </td>
";
        private const string ImageCellTemplate = @"
            <td>
                <img height=""100"" src=""data:image/png;base64, {0}""/>
            </td>
";

        private const string EmptyCellTemplate = @"
            <td>
            </td>
";

        public static void ExportResultsToHtml<T>(
          string filePath,
          T[] results,
          CancellationToken cancellationToken
      )
        {
            var totalCount = results.Length;
            Console.WriteLine($"Exporting {totalCount} rows.");
            var properties = typeof(T)
                .GetProperties()
                .ToList();

            var document = new StringBuilder();
            document.Append(DocumentOpening);

            /* HEADER
             <tr>
                <th width="150">
                    testPicture
                </th>
                <th width="150">
                    testText
                </th>
                <th width="150">
                    testPicture2
                </th>
            </tr>
            */

            document.AppendLine("<tr>");



            foreach (var propertyName in properties.Select(p => p.Name))
            {
                document.AppendLine(string.Format(HeaderColumnTemplate, propertyName));
            }

            document.AppendLine("</tr>");
            var counter = 1;

            foreach (var result in results)
            {
                /* ONE ITEM
            <tr>
                <td>
                    <img height="100" src="1585130523.jpg"/>
                </td>
                <td > test value </td>
                <td>
                    <img height="100" src="./1fa47936-ba4f-41ef-a8fb-62fcf874d98f.jpg"/>
                </td>
            </tr>
                 */
                cancellationToken.ThrowIfCancellationRequested();

                document.AppendLine("<tr>");

                Console.WriteLine($"Writing row {counter}/{totalCount}.");
                foreach (var property in properties)
                {
                    if (property.PropertyType == typeof(byte[]))
                    {
                        var imageData = (byte[])property.GetValue(result);

                        if (imageData != null)
                        {
                            var imageBase64 = Convert.ToBase64String(imageData);
                            document.AppendLine(string.Format(ImageCellTemplate, imageBase64));
                        }
                        else
                        {
                            document.AppendLine(EmptyCellTemplate);
                        }
                    }
                    else
                    {
                        var value = property.GetValue(result);
                        document.AppendLine(string.Format(TextCellTemplate, value));
                        // if (propertiesToFormat.Contains(property.Name) && (int) value >= matchThreshold)
                        // {
                        //     document.AppendLine(string.Format(GreenTextCellTemplate, value));
                        // }
                        // else
                        // {
                        //     document.AppendLine(string.Format(TextCellTemplate, value));
                        // }
                    }
                }

                document.AppendLine("</tr>");
                counter++;
            }

            document.AppendLine(DocumentClosing);

            Console.WriteLine($"Writing html file {filePath}...");
            cancellationToken.ThrowIfCancellationRequested();

            File.WriteAllText(filePath, document.ToString());

            Console.WriteLine($"Writing html file done.");
        }
    }
}