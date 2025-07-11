# Google Calendars Connector

This service integrates SmartFace with Google Calendars to create calendar events when specific stream group activities are detected.

## Features

- **Thread-safe caching**: Reduces Google Calendar API calls by caching overlapping event checks
- **Configurable cache settings**: Adjust cache expiration and size limits
- **Parallel processing**: Supports multiple concurrent operations
- **GraphQL notifications**: Receives real-time notifications from SmartFace

## Configuration

### Cache Settings

The cache can be configured in `appsettings.json`:

```json
{
  "CalendarCache": {
    "ExpirationMinutes": 30,
    "MaxSize": 1000
  }
}
```

- `ExpirationMinutes`: How long cache entries are valid (default: 30 minutes)
- `MaxSize`: Maximum number of cache entries (default: 1000)

### Google Calendar Settings

```json
{
  "GoogleCalendar": {
    "CredentialsPath": "credentials.json",
    "TokenPath": "token.json",
    "DefaultTimeZone": "Europe/Bratislava",
    "DefaultEventDurationHours": 1,
    "MeetingDurationMin": 30
  }
}
```

## Cache Implementation

The `CalendarCacheService` provides thread-safe caching for Google Calendar API calls:

- **ConcurrentDictionary**: Thread-safe cache storage
- **Automatic cleanup**: Expired entries are automatically removed
- **Memory management**: Oldest entries are removed when cache is full
- **Configurable expiration**: Cache entries expire after a configurable time period

### Cache Key Strategy

Cache keys are generated based on:
- Calendar ID
- Start time (rounded to nearest minute)
- End time (rounded to nearest minute)

This ensures that similar time ranges are grouped together, maximizing cache efficiency.

### Benefits

1. **Reduced API calls**: Cached results avoid repeated Google Calendar API calls
2. **Improved performance**: Faster response times for repeated checks
3. **Cost reduction**: Fewer API calls mean lower costs
4. **Rate limit protection**: Reduces the risk of hitting Google API rate limits

## Usage

The cache is automatically used in the `QueueProcessingService` when checking for overlapping events. The service will:

1. First check the cache for existing results
2. If cache miss, call the Google Calendar API
3. Store the result in cache for future use
4. Return the cached or fresh result

## Monitoring

The service provides detailed logging for cache operations:
- Cache hits and misses
- Cache cleanup operations
- Cache size monitoring
- API call reductions 