using System;
using System.Linq;
using Innovatrics.SmartFace.Integrations.DataExportTool.Models.Odata;
using CsvHelper.Configuration.Attributes;

namespace Innovatrics.SmartFace.Integrations.DataExportTool.Models
{
    public class IndividualResult
    {
        public Guid Id { get; init; }

        [Ignore]
        public byte[] FirstFace { get; internal set; }
        public DateTime? EntranceTime { get; init; }
        public string EntranceCamera { get; internal set; }

        [Ignore]
        public byte[] LastFace { get; internal set; }
        public DateTime? ExitTime { get; internal set; }
        public string ExitCamera { get; internal set; }

        public int? Tracklets { get; internal set; }

        public static IndividualResult FromDbResult(Individual model)
        {
            return new IndividualResult
            {
                Id = model.Id,
                EntranceTime = model.EntranceTime,
                ExitTime = model.ExitTime,

                Tracklets = model.Tracklets?.Count
            };
        }
    }
}