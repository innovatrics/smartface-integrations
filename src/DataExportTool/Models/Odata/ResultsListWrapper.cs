using System.Collections.Generic;

namespace ChangiDataExport.Models.Odata
{
    public record ResultsListWrapper<T>
    {
        public List<T> Value { get; init; }
    }
}