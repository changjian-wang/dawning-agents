namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Calendar event.
/// </summary>
public record CalendarEvent
{
    /// <summary>
    /// Unique event identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Event subject / title.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Event start time.
    /// </summary>
    public DateTimeOffset Start { get; init; }

    /// <summary>
    /// Event end time.
    /// </summary>
    public DateTimeOffset End { get; init; }

    /// <summary>
    /// Event location.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Event description / body.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Organizer email address.
    /// </summary>
    public string? Organizer { get; init; }

    /// <summary>
    /// Attendee email addresses.
    /// </summary>
    public IReadOnlyList<string> Attendees { get; init; } = [];

    /// <summary>
    /// Whether the event is an all-day event.
    /// </summary>
    public bool IsAllDay { get; init; }

    /// <summary>
    /// Whether the event is cancelled.
    /// </summary>
    public bool IsCancelled { get; init; }

    /// <summary>
    /// Online meeting URL (e.g., Teams, Zoom link).
    /// </summary>
    public string? OnlineMeetingUrl { get; init; }
}

/// <summary>
/// Parameters for creating a calendar event.
/// </summary>
public record CreateEventRequest
{
    /// <summary>
    /// Event subject.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Event start time.
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// Event end time.
    /// </summary>
    public required DateTimeOffset End { get; init; }

    /// <summary>
    /// Event location.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Event description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Attendee email addresses.
    /// </summary>
    public IReadOnlyList<string> Attendees { get; init; } = [];

    /// <summary>
    /// Whether to create an online meeting.
    /// </summary>
    public bool CreateOnlineMeeting { get; init; }
}

/// <summary>
/// Represents a free/busy time slot.
/// </summary>
public record FreeSlot
{
    /// <summary>
    /// Start of the free slot.
    /// </summary>
    public DateTimeOffset Start { get; init; }

    /// <summary>
    /// End of the free slot.
    /// </summary>
    public DateTimeOffset End { get; init; }
}

/// <summary>
/// Calendar connector — manage events, find free time, and schedule meetings.
/// </summary>
public interface ICalendarConnector : IConnector
{
    /// <summary>
    /// Gets events within a time range.
    /// </summary>
    /// <param name="start">Range start.</param>
    /// <param name="end">Range end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a new calendar event.
    /// </summary>
    /// <param name="request">Event creation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CalendarEvent> CreateEventAsync(
        CreateEventRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Finds free time slots for the specified attendees.
    /// </summary>
    /// <param name="attendees">Attendee email addresses.</param>
    /// <param name="start">Search range start.</param>
    /// <param name="end">Search range end.</param>
    /// <param name="durationMinutes">Required meeting duration in minutes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<FreeSlot>> FindFreeSlotsAsync(
        IEnumerable<string> attendees,
        DateTimeOffset start,
        DateTimeOffset end,
        int durationMinutes = 30,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates an existing calendar event.
    /// </summary>
    /// <param name="eventId">Event ID.</param>
    /// <param name="request">Updated event properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CalendarEvent> UpdateEventAsync(
        string eventId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cancels an event.
    /// </summary>
    /// <param name="eventId">Event ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelEventAsync(string eventId, CancellationToken cancellationToken = default);
}
