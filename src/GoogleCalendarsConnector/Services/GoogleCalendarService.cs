using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartFace.GoogleCalendarsConnector.Service
{
    public class GoogleCalendarService
    {
        private readonly CalendarService _calendarService;

        public GoogleCalendarService(string credentialsPath = "credentials.json", string tokenPath = "token.json")
        {
            var credential = Authenticate(credentialsPath, tokenPath).Result;

            _calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Calendar API Wrapper"
            });
        }

        private async Task<UserCredential> Authenticate(string credentialsPath, string tokenPath)
        {
            using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);

            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                new[] { CalendarService.Scope.Calendar },
                "user",
                CancellationToken.None,
                new FileDataStore(tokenPath, true)
            );
        }

        public async Task<string> CreateMeetingAsync(string summary, string description, string location, DateTime start, DateTime end, string[] attendeesEmails)
        {
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

            var request = _calendarService.Events.Insert(newEvent, "primary");
            var createdEvent = await request.ExecuteAsync();

            return createdEvent.Id; // or return createdEvent.HtmlLink for the calendar URL
        }

        public async Task DeleteMeetingAsync(string eventId)
        {
            await _calendarService.Events.Delete("primary", eventId).ExecuteAsync();
        }

        public async Task CreateEventAsync(string groupName)
        {
            // Create a simple event for the stream group
            var summary = $"Stream Group Activity: {groupName}";
            var description = $"Activity detected in stream group {groupName}";
            var location = "SmartFace System";
            var start = DateTime.UtcNow;
            var end = start.AddHours(1);
            var attendees = new string[] { }; // No attendees for now

            await CreateMeetingAsync(summary, description, location, start, end, attendees);
        }
    }
}
