﻿namespace ChangiDataExport.Models
{
    public record WatchlistCreateRequest
    {
        public string DisplayName { get; init; }

        public string FullName { get; init; }

        public int Threshold { get; init; }
    }
}
