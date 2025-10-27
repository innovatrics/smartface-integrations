using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using Innovatrics.SmartFace.Integrations.AEOS.SmartFaceClients;
using ServiceReference;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Innovatrics.SmartFace.Integrations.LockerMailer.DataModels;
using System.Net.Mail;

namespace Innovatrics.SmartFace.Integrations.LockerMailer
{
    public class SmtpMailAdapter : ISmtpMailAdapter
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        private string smtpHost = string.Empty;
        private int smtpPort;
        private string smtpUser = string.Empty;
        private string smtpPass = string.Empty;
        private string fromEmail = string.Empty;

        public SmtpMailAdapter(
            ILogger logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            this.logger.Information("SmtpMail DataAdapter Initiated");

            // Read SMTP config
            smtpHost = configuration.GetValue<string>("LockerMailer:Connections:SmtpMailGateway:Host") ?? string.Empty;
            smtpPort = configuration.GetValue<int>("LockerMailer:Connections:SmtpMailGateway:Port", 25);
            smtpUser = configuration.GetValue<string>("LockerMailer:Connections:SmtpMailGateway:User") ?? string.Empty;
            smtpPass = configuration.GetValue<string>("LockerMailer:Connections:SmtpMailGateway:Pass") ?? string.Empty;
            fromEmail = configuration.GetValue<string>("LockerMailer:Connections:SmtpMailGateway:FromEmail") ?? "no-reply@local.test";
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new ArgumentException("Recipient email must be provided", nameof(toEmail));
            }

            try
            {
                // Resolve and log SMTP host addresses for diagnostics
                try
                {
                    var addresses = System.Net.Dns.GetHostAddresses(smtpHost).Select(a => a.ToString()).ToArray();
                    this.logger.Debug($"SMTP host '{smtpHost}' resolved to: {string.Join(", ", addresses)}");
                }
                catch (Exception dnsEx)
                {
                    this.logger.Warning(dnsEx, $"Failed to resolve SMTP host '{smtpHost}'");
                }

                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject ?? string.Empty;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = htmlBody ?? string.Empty;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = true;

                using var smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                // International delivery format to avoid ASCII-only issues
                try { smtp.DeliveryFormat = SmtpDeliveryFormat.International; } catch { /* ignore for older frameworks */ }

                var usingCreds = false;
                if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
                {
                    smtp.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);
                    usingCreds = true;
                }

                this.logger.Information($"Sending email to {toEmail} via {smtpHost}:{smtpPort} (ssl: false, auth: {usingCreds})");
                await smtp.SendMailAsync(message);
            }
            catch (SmtpException ex)
            {
                // Extract deeper diagnostics
                var inner = ex.InnerException;
                if (inner is System.Net.Sockets.SocketException sock)
                {
                    this.logger.Error(ex, $"SMTP SocketException Host={smtpHost} Port={smtpPort} ErrorCode={sock.SocketErrorCode} Native={sock.ErrorCode}");
                }
                else if (inner != null)
                {
                    this.logger.Error(ex, $"SMTP inner exception: {inner.GetType().Name}: {inner.Message}");
                }
                else
                {
                    this.logger.Error(ex, "SMTP exception without inner error");
                }

                throw;
            }
        }
    }
}
