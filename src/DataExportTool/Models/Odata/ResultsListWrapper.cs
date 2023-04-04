using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata
{
    public record ResultsListWrapper<T>
    {
        public List<T> Value { get; init; }
    }
}