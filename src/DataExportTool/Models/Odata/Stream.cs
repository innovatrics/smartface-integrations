using System;

namespace ChangiDataExport.Models.Odata
{
    public record Stream
    {
        public Guid? Id { get; init; }
        public string Name { get; init; }
    }
}