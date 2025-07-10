using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

using Serilog;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class GoogleCalendarService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private CalendarService _calendarService;

        private readonly int _meetingDurationMin = 30;

        public GoogleCalendarService(
            ILogger logger,
            IConfiguration configuration
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _meetingDurationMin = configuration.GetValue("GoogleCalendar:MeetingDurationMin", 30);

            InitializeCalendarService();
        }

        public async Task DeleteMeetingAsync(string eventId)
        {
            EnsureCalendarIsInitialized();

            await _calendarService.Events.Delete("primary", eventId).ExecuteAsync();
        }

        public async Task CreateEventAsync(string groupName, string[] attendeesEmails)
        {
            // Create a simple event for the stream group
            var summary = $"Stream Group Activity: {groupName}";
            var description = $"Activity detected in stream group {groupName}";
            var location = "SmartFace System";
            var start = DateTime.UtcNow;
            var end = start.AddMinutes(_meetingDurationMin);
            var attendees = attendeesEmails ?? new string[] { };

            await CreateMeetingAsync(summary, description, location, start, end, attendees);
        }

        public async Task<bool> HasOverlappingEventAsync(string calendarId, DateTime start, DateTime end)
        {
            EnsureCalendarIsInitialized();

            var eventsListRequest = _calendarService.Events.List(calendarId);
            // eventsListRequest.TimeMin = start;
            // eventsListRequest.TimeMax = end;
            // eventsListRequest.ShowDeleted = false;
            // eventsListRequest.SingleEvents = true;
            // eventsListRequest.MaxResults = 10;
            // eventsListRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await eventsListRequest.ExecuteAsync();

            var eventsInRange = events.Items.Any(e => e.Start.DateTimeDateTimeOffset >= start && e.End.DateTimeDateTimeOffset <= end);

            return eventsInRange;
        }

        private void EnsureCalendarIsInitialized()
        {
            if (_calendarService == null)
            {
                InitializeCalendarService();
            }
        }

        private void InitializeCalendarService()
        {
            _logger.Information("Initializing calendar service");

            var credentialsPath = _configuration.GetValue<string>("GoogleCalendar:CredentialsPath", "credentials.json");
            var tokenPath = _configuration.GetValue<string>("GoogleCalendar:TokenPath", "token.json");

            var credential = Authenticate(credentialsPath, tokenPath).Result;

            _calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Calendar API Wrapper"
            });
        }
                
        private async Task<GoogleCredential> Authenticate(string credentialsPath, string tokenPath)
        {
            _logger.Information("Authenticating with credentials path: {CredentialsPath} and token path: {TokenPath}", credentialsPath, tokenPath);

            var credential = GoogleCredential
                                .FromFile(credentialsPath)
                                .CreateScoped(CalendarService.Scope.Calendar);

            return credential;
        }

        private async Task<string> CreateMeetingAsync(string calendarId, string summary, string description, string location, DateTime start, DateTime end, string[] attendeesEmails)
        {
            EnsureCalendarIsInitialized();

            var newEvent = new Event
            {
                Summary = summary,
                Description = description,
                Location = location,
                Start = new EventDateTime
                {
                    DateTimeDateTimeOffset = start,
                    TimeZone = "Europe/Bratislava"
                },
                End = new EventDateTime
                {
                    DateTimeDateTimeOffset = end,
                    TimeZone = "Europe/Bratislava"
                },
                Attendees = Array.ConvertAll(attendeesEmails, email => new EventAttendee { Email = email }),
                Reminders = new Event.RemindersData
                {
                    UseDefault = true
                }
            };

            _logger.Information("Creating meeting: {Summary}, {Description}, {Location}, {Start}, {End}, {@Attendees}", summary, description, location, start, end, attendeesEmails);

            var request = _calendarService.Events.Insert(newEvent, calendarId);
            var createdEvent = await request.ExecuteAsync();

            _logger.Information("Meeting created: {Id}", createdEvent.Id);

            return createdEvent.Id; // or return createdEvent.HtmlLink for the calendar URL
        }
    }
}
