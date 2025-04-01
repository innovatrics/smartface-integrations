using System;

namespace Innovatrics.SmartFace.DataDownload
{
    public class PalmsResponse
    {
        public GenericObjects GenericObjects { get; set; }
    }

    public class GenericObjects
    {
        public GenericObject[] Items { get; set; }

        public int TotalCount { get; set; }

        public PageInfo PageInfo { get; set; }
    }

    public class GenericObject
    {
        public string Id { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string StreamId { get; set; }
        public int ObjectType { get; set; }
        public Frame Frame { get; set; }
        public Guid? ImageDataId { get; set; }
    }

    public class Frame
    {
        public string Id { get; set; }
        public Guid? ImageDataId { get; set; }
    }

    public class PageInfo
    {
        public bool HasNextPage { get; set; }
    }
}