using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Services
{
    public class MailLoggingService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly string baseLogDirectory;

        public MailLoggingService(ILogger logger, IConfiguration configuration)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Get base log directory from configuration
            var logDirectory = configuration.GetValue<string>("Serilog:LogDirectory");
            if (string.IsNullOrEmpty(logDirectory))
            {
                var commonAppDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create);
                logDirectory = Path.Combine(commonAppDataDirPath, "Innovatrics", "SmartFace2LockerMailer");
            }
            
            this.baseLogDirectory = Path.Combine(logDirectory, "MailLogs");
        }

        public void LogEmail(
            string templateUsed,
            string employeeName,
            int? employeeId,
            string fromEmail,
            string toEmail,
            string subject,
            string content,
            Dictionary<string, string?> variableDump)
        {
            try
            {
                var timestamp = DateTime.Now;
                var monthFolder = timestamp.ToString("yyyy-MM");
                var monthDirectory = Path.Combine(baseLogDirectory, monthFolder);

                // Create directory if it doesn't exist
                if (!Directory.Exists(monthDirectory))
                {
                    Directory.CreateDirectory(monthDirectory);
                    logger.Debug($"Created mail log directory: {monthDirectory}");
                }

                // Create filename with timestamp including milliseconds
                var fileName = $"{timestamp:yyyy-MM-dd_HH-mm-ss-fff}.json";
                var filePath = Path.Combine(monthDirectory, fileName);

                // Create log entry object
                var logEntry = new MailLogEntry
                {
                    Timestamp = timestamp,
                    TemplateUsed = templateUsed,
                    EmployeeName = employeeName,
                    EmployeeId = employeeId,
                    FromEmail = fromEmail,
                    ToEmail = toEmail,
                    Subject = subject,
                    Content = content,
                    VariableDump = variableDump ?? new Dictionary<string, string?>()
                };

                // Serialize to JSON
                var json = JsonConvert.SerializeObject(logEntry, Formatting.Indented);

                // Write to file
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                logger.Debug($"Mail log written to: {filePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to log email to file. Template: {templateUsed}, To: {toEmail}");
            }
        }
    }

    public class MailLogEntry
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("templateUsed")]
        public string TemplateUsed { get; set; } = string.Empty;

        [JsonProperty("employeeName")]
        public string EmployeeName { get; set; } = string.Empty;

        [JsonProperty("employeeId")]
        public int? EmployeeId { get; set; }

        [JsonProperty("fromEmail")]
        public string FromEmail { get; set; } = string.Empty;

        [JsonProperty("toEmail")]
        public string ToEmail { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        [JsonProperty("variableDump")]
        public Dictionary<string, string?> VariableDump { get; set; } = new Dictionary<string, string?>();
    }
}

