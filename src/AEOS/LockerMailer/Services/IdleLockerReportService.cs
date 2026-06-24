using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Services
{
    /// <summary>
    /// Background service that, once per day at a configured time, emails a report of the
    /// assigned lockers in the configured "changing room" groups that have not been opened
    /// for longer than a configured number of days. The report is sent to a dedicated
    /// recipient list (e.g. reception) and does NOT release any lockers.
    ///
    /// Configuration section: <c>LockerMailer:IdleLockerReport</c>
    ///   Enabled      - bool, master switch (default false)
    ///   TriggerTime  - "HH:mm" local time the report is sent (default 09:00)
    ///   IdleDays     - lockers idle for strictly more than this many days are reported (default 14)
    ///   CheckGroups  - locker group names to inspect (e.g. "Change Room Gents", "Change Room Ladies")
    ///   Recipients   - email addresses the report is sent to
    /// </summary>
    public class IdleLockerReportService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IDashboardsDataAdapter dashboardsDataAdapter;
        private readonly ISmtpMailAdapter smtpMailAdapter;

        private readonly bool isEnabled;
        private readonly TimeSpan triggerTime;
        private readonly int idleDays;
        private readonly List<string> checkGroups;
        private readonly List<string> recipients;

        // Guards against sending more than once on the same day.
        private DateTime? lastSentDate;

        // After TriggerTime, keep retrying a failed run for up to this many minutes before
        // giving up for the day. Bounds retries so a long outage can't spin forever, while a
        // brief Dashboards/SMTP blip no longer silently drops the day's report.
        private const double RetryWindowMinutes = 60;

        public IdleLockerReportService(
            ILogger logger,
            IConfiguration configuration,
            IDashboardsDataAdapter dashboardsDataAdapter,
            ISmtpMailAdapter smtpMailAdapter
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.dashboardsDataAdapter = dashboardsDataAdapter ?? throw new ArgumentNullException(nameof(dashboardsDataAdapter));
            this.smtpMailAdapter = smtpMailAdapter ?? throw new ArgumentNullException(nameof(smtpMailAdapter));

            var section = configuration.GetSection("LockerMailer:IdleLockerReport");

            isEnabled = section.GetValue<bool>("Enabled", false);
            idleDays = section.GetValue<int>("IdleDays", 14);

            var triggerTimeRaw = section.GetValue<string>("TriggerTime") ?? "09:00";
            if (TimeSpan.TryParse(triggerTimeRaw, CultureInfo.InvariantCulture, out var parsedTime))
            {
                triggerTime = parsedTime;
            }
            else
            {
                triggerTime = new TimeSpan(9, 0, 0);
                logger.Warning($"[IdleLockerReportService] Invalid TriggerTime '{triggerTimeRaw}' - defaulting to 09:00");
            }

            checkGroups = section.GetSection("CheckGroups").Get<List<string>>() ?? new List<string>();
            recipients = section.GetSection("Recipients").Get<List<string>>() ?? new List<string>();

            if (!isEnabled)
            {
                logger.Information("[IdleLockerReportService] Disabled (LockerMailer:IdleLockerReport:Enabled=false)");
            }
            else
            {
                logger.Information($"[IdleLockerReportService] Enabled - TriggerTime: {triggerTime:hh\\:mm}, IdleDays: {idleDays}, CheckGroups: [{string.Join(", ", checkGroups)}], Recipients: {recipients.Count}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!isEnabled)
            {
                return;
            }

            if (!checkGroups.Any())
            {
                logger.Warning("[IdleLockerReportService] No CheckGroups configured - service will not run");
                return;
            }

            if (!recipients.Any())
            {
                logger.Warning("[IdleLockerReportService] No Recipients configured - service will not run");
                return;
            }

            logger.Information("[IdleLockerReportService] Started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    // Fire only at or after the configured time (never early), and keep retrying
                    // within the retry window until a run actually succeeds.
                    var minutesSinceTrigger = (now.TimeOfDay - triggerTime).TotalMinutes;
                    var withinWindow = minutesSinceTrigger >= 0 && minutesSinceTrigger <= RetryWindowMinutes;
                    var alreadySentToday = lastSentDate.HasValue && lastSentDate.Value.Date == now.Date;

                    if (withinWindow && !alreadySentToday)
                    {
                        logger.Information($"[IdleLockerReportService] Running idle locker report ({now:HH:mm})");
                        // Only mark the day done when the run completed (report sent, or genuinely
                        // nothing to report after a successful fetch). On a transient failure
                        // lastSentDate stays unset so the next loop retries within the window.
                        if (await BuildAndSendReport())
                        {
                            lastSentDate = now;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.Information("[IdleLockerReportService] Service is shutting down");
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "[IdleLockerReportService] Error in idle locker report loop");
                    // Back off briefly so a persistent failure does not hot-loop.
                    try { await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        /// <summary>
        /// Builds and sends the report. Returns true when the day is "done" — report sent, or
        /// genuinely nothing to report after a successful fetch. Returns false on a transient
        /// failure (no groups fetched, or every send failed) so the caller retries within the window.
        /// </summary>
        private async Task<bool> BuildAndSendReport()
        {
            // 1. Pull all groups (with per-locker last-used data) from the Dashboards API.
            var groups = await dashboardsDataAdapter.GetGroups();
            if (!groups.Any())
            {
                logger.Warning("[IdleLockerReportService] No groups returned from Dashboards API - will retry");
                return false;
            }

            // 2. Collect assigned lockers in the configured groups that are idle beyond the threshold.
            var idleLockers = new List<IdleLockerRow>();
            var matchedGroups = 0;
            foreach (var groupName in checkGroups)
            {
                var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    logger.Warning($"[IdleLockerReportService] Configured group '{groupName}' not found in Dashboards response");
                    continue;
                }

                matchedGroups++;

                var matches = group.AllLockers
                    .Where(l => l.AssignedTo.HasValue && !string.IsNullOrWhiteSpace(l.AssignedEmployeeName))
                    // Strictly more than the idle threshold. Never-opened lockers (LastUsed null,
                    // DaysSinceLastUse 0 upstream) are intentionally NOT reported: with no assignment
                    // timestamp we cannot tell a freshly-assigned locker from a long-idle one, so
                    // flagging every just-assigned locker would be a daily false positive.
                    .Where(l => l.DaysSinceLastUse > idleDays)
                    .Select(l => new IdleLockerRow
                    {
                        LockerName = l.Name,
                        AssignedTo = l.AssignedEmployeeName,
                        LastUsed = l.LastUsed,
                        DaysSinceLastUse = l.DaysSinceLastUse
                    });

                idleLockers.AddRange(matches);
            }

            if (matchedGroups == 0)
            {
                logger.Warning($"[IdleLockerReportService] None of the configured groups [{string.Join(", ", checkGroups)}] were present in the Dashboards response - will retry");
                return false;
            }

            if (!idleLockers.Any())
            {
                logger.Information($"[IdleLockerReportService] No assigned lockers idle beyond {idleDays} days in [{string.Join(", ", checkGroups)}] - nothing to send");
                return true;
            }

            // Most idle first - the most actionable rows are at the top.
            idleLockers = idleLockers.OrderByDescending(r => r.DaysSinceLastUse).ToList();
            logger.Information($"[IdleLockerReportService] Found {idleLockers.Count} idle locker(s) - composing email");

            // 3. Build the email.
            var today = DateTime.Now;
            var plural = idleLockers.Count == 1 ? "" : "s";
            var subject = $"Idle changing-room lockers — {today:d MMM yyyy} ({idleLockers.Count} locker{plural})";
            var htmlBody = BuildHtml(idleLockers, today);

            var debugMode = configuration.GetValue<bool>("LockerMailer:DebugMode", false);
            if (debugMode)
            {
                logger.Information("[IdleLockerReportService] DebugMode enabled - email not sent. Generated HTML:\n" + htmlBody);
                return true;
            }

            // 4. Send to each configured recipient. Track successes so that a total failure
            //    (e.g. SMTP down) reports back as "not done" and the caller retries.
            var sentCount = 0;
            foreach (var recipient in recipients)
            {
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    continue;
                }

                try
                {
                    var loggingData = new MailLoggingData
                    {
                        TemplateUsed = "idle-locker-report",
                        EmployeeName = "Idle Locker Report",
                        EmployeeId = null,
                        VariableDump = new Dictionary<string, string?>
                        {
                            { "idleDays", idleDays.ToString() },
                            { "lockerCount", idleLockers.Count.ToString() },
                            { "groups", string.Join(", ", checkGroups) }
                        }
                    };

                    await smtpMailAdapter.SendAsync(recipient, subject, htmlBody, loggingData);
                    sentCount++;
                    logger.Information($"[IdleLockerReportService] Idle locker report sent to {recipient} ({idleLockers.Count} locker(s))");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"[IdleLockerReportService] Failed to send idle locker report to {recipient}");
                }
            }

            if (sentCount == 0)
            {
                logger.Warning("[IdleLockerReportService] Idle locker report reached no recipients - will retry");
                return false;
            }

            return true;
        }

        private string BuildHtml(List<IdleLockerRow> rows, DateTime today)
        {
            const string cell = "padding:6px 12px;border:1px solid #ddd;";
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html><body style=\"font-family:Arial,Helvetica,sans-serif;color:#222;\">");
            sb.Append($"<p>The following assigned changing-room lockers have not been opened in more than {idleDays} days (as of {today:d MMM yyyy}):</p>");
            sb.Append("<table style=\"border-collapse:collapse;\">");
            sb.Append("<thead><tr style=\"background:#f0f0f0;text-align:left;\">");
            sb.Append($"<th style=\"{cell}\">Locker</th>");
            sb.Append($"<th style=\"{cell}\">Assigned to</th>");
            sb.Append($"<th style=\"{cell}\">Last used</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                sb.Append($"<td style=\"{cell}\">{Escape(row.LockerName)}</td>");
                sb.Append($"<td style=\"{cell}\">{Escape(row.AssignedTo)}</td>");
                sb.Append($"<td style=\"{cell}\">{Escape(FormatLastUsed(row.LastUsed, today))}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append($"<p style=\"color:#888;font-size:12px;\">Automated report — {rows.Count} locker(s) across {Escape(string.Join(", ", checkGroups))}.</p>");
            sb.Append("</body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// Renders the elapsed time since <paramref name="lastUsed"/> as a human-friendly,
        /// calendar-accurate diff such as "2 months 3 days ago (21 Apr 2026)".
        /// </summary>
        private static string FormatLastUsed(DateTime? lastUsed, DateTime now)
        {
            if (!lastUsed.HasValue)
            {
                return "Never used";
            }

            var last = lastUsed.Value;
            if (last >= now)
            {
                return "Today";
            }

            // Calendar-accurate years / months / days difference. Anchor on `last` and add whole
            // months, then count the leftover days. AddMonths clamps short months (e.g. Jan 30 +
            // 1 month = Feb 28), which avoids the negative-borrow bug a manual day subtraction hits.
            int totalMonths = (now.Year - last.Year) * 12 + (now.Month - last.Month);
            var anchor = last.AddMonths(totalMonths);
            if (anchor > now)
            {
                totalMonths--;
                anchor = last.AddMonths(totalMonths);
            }
            int years = totalMonths / 12;
            int months = totalMonths % 12;
            int days = (now.Date - anchor.Date).Days;

            var parts = new List<string>();
            if (years > 0) parts.Add($"{years} year{(years == 1 ? "" : "s")}");
            if (months > 0) parts.Add($"{months} month{(months == 1 ? "" : "s")}");
            if (days > 0) parts.Add($"{days} day{(days == 1 ? "" : "s")}");
            if (parts.Count == 0) parts.Add("less than a day");

            return $"{string.Join(" ", parts)} ago ({last:d MMM yyyy})";
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class IdleLockerRow
        {
            public string LockerName { get; set; } = string.Empty;
            public string AssignedTo { get; set; } = string.Empty;
            public DateTime? LastUsed { get; set; }
            public double DaysSinceLastUse { get; set; }
        }
    }
}
