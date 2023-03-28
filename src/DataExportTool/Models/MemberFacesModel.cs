using System;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class MemberFacesModel
    {
        public List<MemberFace> Items { get; set; }
    }

    public class MemberFace
    {
        public Guid ImageDataId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}