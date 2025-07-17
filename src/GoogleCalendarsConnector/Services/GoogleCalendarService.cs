using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

using SmartFace.GoogleCalendarsConnector.Models;

namespace SmartFace.GoogleCalendarsConnector.Services
{
    public class GoogleCalendarService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private CalendarService _calendarService;

        private readonly int _meetingDurationMin;
        private readonly string _timeZone;
        private readonly int _lookbackHours;
        private readonly int _lookaheadHours;
        private readonly string _applicationName;

        public GoogleCalendarService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _meetingDurationMin = configuration.GetValue("GoogleCalendar:MeetingDurationMin", 30);
            _timeZone = configuration.GetValue("GoogleCalendar:TimeZone", "Europe/Bratislava");
            _applicationName = configuration.GetValue("GoogleCalendar:ApplicationName", "Google Calendar API");

            _lookbackHours = configuration.GetValue("GoogleCalendar:EventsLookbackHours", 24);
            _lookaheadHours = configuration.GetValue("GoogleCalendar:EventsLookaheadHours", 24);

            InitializeCalendarService();
        }

        public async Task DeleteMeetingAsync(string calendarId, string eventId)
        {
            EnsureCalendarIsInitialized();

            await _calendarService.Events.Delete(calendarId, eventId).ExecuteAsync();
        }

        public async Task UpdateMeetingEndTimeAsync(string calendarId, string eventId, DateTime newEnd)
        {
            EnsureCalendarIsInitialized();

            var existing = await _calendarService.Events.Get(calendarId, eventId).ExecuteAsync();
            existing.End = new EventDateTime
            {
                DateTimeDateTimeOffset = newEnd,
                TimeZone = _timeZone
            };

            await _calendarService.Events.Update(existing, calendarId, eventId).ExecuteAsync();
        }

        public async Task<string> CreateMeetingAsync(string calendarId, string summary, string description, string location, DateTime start, DateTime end, string[] attendeesEmails)
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
                    TimeZone = _timeZone
                },
                End = new EventDateTime
                {
                    DateTimeDateTimeOffset = end,
                    TimeZone = _timeZone
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

            return createdEvent.Id;
        }

        public async Task<GoogleCalendarEvent[]> GetOverlappingEventsAsync(string calendarId, DateTimeOffset start, DateTimeOffset end)
        {
            EnsureCalendarIsInitialized();

            var eventsListRequest = _calendarService.Events.List(calendarId);
            eventsListRequest.TimeMinDateTimeOffset = start.AddHours(-_lookbackHours);
            eventsListRequest.TimeMaxDateTimeOffset = end.AddHours(_lookaheadHours);
            eventsListRequest.ShowDeleted = true;
            eventsListRequest.SingleEvents = true;
            eventsListRequest.MaxResults = 100;
            eventsListRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await eventsListRequest.ExecuteAsync();

            _logger.Information("Events: {@Events}", events.Items.Select(e => new { e.Summary, e.Start, e.End }));

            var eventsInRange = events.Items
                .Where(e =>
                    e.Start.DateTimeDateTimeOffset != null && e.End.DateTimeDateTimeOffset != null &&
                    e.Start.DateTimeDateTimeOffset < end && e.End.DateTimeDateTimeOffset > start
                )
                .Select(e => new GoogleCalendarEvent
                {
                    Start = e.Start.DateTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
                    End = e.End.DateTimeDateTimeOffset?.DateTime ?? DateTime.MinValue,
                    Summary = e.Summary,
                    Description = e.Description,
                    Location = e.Location,
                    Attendees = e.Attendees?.Select(a => a.Email).ToArray() ?? Array.Empty<string>(),
                    EventId = e.Id
                })
                .ToArray();

            return eventsInRange;
        }

        private void EnsureCalendarIsInitialized()
        {
            if (_calendarService == null)
                InitializeCalendarService();
        }

        private void InitializeCalendarService()
        {
            _logger.Information("Initializing calendar service");

            var credentialsPath = _configuration.GetValue("GoogleCalendar:CredentialsPath", "credentials.json");
            var tokenPath = _configuration.GetValue("GoogleCalendar:TokenPath", "token.json");

            var credential = Authenticate(credentialsPath, tokenPath);

            _calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });
        }

        private GoogleCredential Authenticate(string credentialsPath, string tokenPath)
        {
            _logger.Information("Authenticating with credentials path: {CredentialsPath} and token path: {TokenPath}", credentialsPath, tokenPath);

            var credential = GoogleCredential
                                .FromFile(credentialsPath)
                                .CreateScoped(CalendarService.Scope.Calendar);

            var serviceUser = _configuration.GetValue<string>("GoogleCalendar:ServiceUser");

            if (!string.IsNullOrEmpty(serviceUser))
            {
                credential = credential.CreateWithUser(serviceUser);
            }

            return credential;
        }
    }
}
