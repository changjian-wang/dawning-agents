using Dawning.Agents.Abstractions.Connectors;
using FluentAssertions;

namespace Dawning.Agents.Tests.Connectors;

/// <summary>
/// Connector model and options validation tests.
/// </summary>
public sealed class ConnectorModelsTests
{
    #region ConnectorOptions

    [Fact]
    public void ConnectorOptions_WithApiKey_ShouldNotThrow()
    {
        var options = new ConnectorOptions { ApiKey = "test-key" };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void ConnectorOptions_WithOAuth_ShouldNotThrow()
    {
        var options = new ConnectorOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void ConnectorOptions_WithNoCredentials_ShouldThrow()
    {
        var options = new ConnectorOptions();
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*ClientId*");
    }

    #endregion

    #region EmailModels

    [Fact]
    public void EmailMessage_DefaultProperties_ShouldHaveEmptyCollections()
    {
        var msg = new EmailMessage
        {
            Id = "1",
            Subject = "Test",
            From = "a@b.com",
        };

        msg.To.Should().BeEmpty();
        msg.Cc.Should().BeEmpty();
        msg.Body.Should().BeNull();
        msg.IsRead.Should().BeFalse();
    }

    [Fact]
    public void EmailQuery_DefaultValues_ShouldBeSensible()
    {
        var query = new EmailQuery();
        query.MaxResults.Should().Be(10);
        query.UnreadOnly.Should().BeNull();
    }

    [Fact]
    public void DraftEmail_RequiredProperties_ShouldBeSet()
    {
        var draft = new DraftEmail
        {
            To = ["x@y.com"],
            Subject = "Hello",
            Body = "World",
        };

        draft.To.Should().HaveCount(1);
        draft.Cc.Should().BeEmpty();
        draft.IsHtml.Should().BeFalse();
    }

    #endregion

    #region CalendarModels

    [Fact]
    public void CalendarEvent_DefaultProperties_ShouldHaveEmptyAttendees()
    {
        var evt = new CalendarEvent { Id = "1", Subject = "Meeting" };

        evt.Attendees.Should().BeEmpty();
        evt.IsAllDay.Should().BeFalse();
        evt.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void CreateEventRequest_RequiredProperties_ShouldBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var req = new CreateEventRequest
        {
            Subject = "Standup",
            Start = now,
            End = now.AddMinutes(30),
        };

        req.Attendees.Should().BeEmpty();
        req.CreateOnlineMeeting.Should().BeFalse();
    }

    [Fact]
    public void UpdateEventRequest_AllFieldsOptional()
    {
        var req = new UpdateEventRequest();

        req.Subject.Should().BeNull();
        req.Start.Should().BeNull();
        req.End.Should().BeNull();
        req.Location.Should().BeNull();
        req.Attendees.Should().BeNull();
        req.CreateOnlineMeeting.Should().BeNull();
    }

    #endregion

    #region KnowledgeBaseModels

    [Fact]
    public void KnowledgeDocument_DefaultProperties_ShouldHaveEmptyTags()
    {
        var doc = new KnowledgeDocument { Id = "doc1", Title = "Architecture Guide" };

        doc.Tags.Should().BeEmpty();
        doc.Content.Should().BeNull();
    }

    [Fact]
    public void KnowledgeSearchResult_ShouldHoldDocumentAndScore()
    {
        var doc = new KnowledgeDocument { Id = "1", Title = "Test" };
        var result = new KnowledgeSearchResult
        {
            Document = doc,
            Score = 0.85f,
            Snippet = "...test snippet...",
        };

        result.Document.Id.Should().Be("1");
        result.Score.Should().BeApproximately(0.85f, 0.001f);
    }

    #endregion
}
