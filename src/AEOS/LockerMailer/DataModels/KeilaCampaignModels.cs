using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.DataModels
{
    public class KeilaCampaignsResponse
    {
        [JsonProperty("data")]
        public List<KeilaCampaign> Data { get; set; } = new List<KeilaCampaign>();
        
        [JsonProperty("meta")]
        public KeilaMeta Meta { get; set; } = new KeilaMeta();
    }

    public class KeilaCampaign
    {
        [JsonProperty("data")]
        public object? Data { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("inserted_at")]
        public DateTime InsertedAt { get; set; }
        
        [JsonProperty("json_body")]
        public KeilaJsonBody? JsonBody { get; set; }
        
        [JsonProperty("mjml_body")]
        public object? MjmlBody { get; set; }
        
        [JsonProperty("scheduled_for")]
        public DateTime? ScheduledFor { get; set; }
        
        [JsonProperty("segment_id")]
        public string? SegmentId { get; set; }
        
        [JsonProperty("sender_id")]
        public string SenderId { get; set; } = string.Empty;
        
        [JsonProperty("sent_at")]
        public DateTime? SentAt { get; set; }
        
        [JsonProperty("settings")]
        public KeilaSettings? Settings { get; set; }
        
        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;
        
        [JsonProperty("template_id")]
        public string? TemplateId { get; set; }
        
        [JsonProperty("text_body")]
        public string TextBody { get; set; } = string.Empty;
        
        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class KeilaJsonBody
    {
        [JsonProperty("blocks")]
        public List<KeilaBlock>? Blocks { get; set; }
        
        [JsonProperty("time")]
        public long Time { get; set; }
        
        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class KeilaBlock
    {
        [JsonProperty("data")]
        public KeilaBlockData? Data { get; set; }
        
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class KeilaBlockData
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class KeilaSettings
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }

    public class KeilaMeta
    {
        [JsonProperty("count")]
        public int Count { get; set; }
        
        [JsonProperty("page")]
        public int Page { get; set; }
        
        [JsonProperty("page_count")]
        public int PageCount { get; set; }
    }
}
