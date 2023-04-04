using System;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata
{
    public record Stream
    {
        public Guid? Id { get; init; }
        public string Name { get; init; }
    }
}