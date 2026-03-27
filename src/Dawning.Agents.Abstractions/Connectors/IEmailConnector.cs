namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Email message summary.
/// </summary>
public record EmailMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Email subject line.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Sender email address.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Recipient email addresses.
    /// </summary>
    public IReadOnlyList<string> To { get; init; } = [];

    /// <summary>
    /// CC email addresses.
    /// </summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>
    /// Email body (plain text).
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Email body (HTML).
    /// </summary>
    public string? BodyHtml { get; init; }

    /// <summary>
    /// When the message was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Whether the message has been read.
    /// </summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// Whether the message has attachments.
    /// </summary>
    public bool HasAttachments { get; init; }
}

/// <summary>
/// Email query parameters.
/// </summary>
public record EmailQuery
{
    /// <summary>
    /// Free-text search query.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Filter by sender address.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// Only return unread messages.
    /// </summary>
    public bool? UnreadOnly { get; init; }

    /// <summary>
    /// Messages received after this time.
    /// </summary>
    public DateTimeOffset? After { get; init; }

    /// <summary>
    /// Messages received before this time.
    /// </summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>
    /// Maximum number of messages to return.
    /// </summary>
    public int MaxResults { get; init; } = 10;
}

/// <summary>
/// Draft email parameters.
/// </summary>
public record DraftEmail
{
    /// <summary>
    /// Recipient addresses.
    /// </summary>
    public required IReadOnlyList<string> To { get; init; }

    /// <summary>
    /// CC addresses.
    /// </summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>
    /// Email subject.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Email body (plain text or HTML).
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Whether the body is HTML.
    /// </summary>
    public bool IsHtml { get; init; }

    /// <summary>
    /// Original message ID (for replies).
    /// </summary>
    public string? InReplyTo { get; init; }
}

/// <summary>
/// Email connector — read, search, and draft emails.
/// </summary>
public interface IEmailConnector : IConnector
{
    /// <summary>
    /// Lists recent email messages.
    /// </summary>
    /// <param name="query">Query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<EmailMessage>> ListMailsAsync(
        EmailQuery? query = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Reads a single email message by ID.
    /// </summary>
    /// <param name="messageId">Message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<EmailMessage> ReadMailAsync(
        string messageId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends an email.
    /// </summary>
    /// <param name="draft">Draft email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMailAsync(DraftEmail draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches emails with a free-text query.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="maxResults">Maximum results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<EmailMessage>> SearchMailAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default
    );
}
