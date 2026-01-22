using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;

namespace Dawning.Agents.Tests.HumanLoop;

public class ConfirmationRequestTests
{
    [Fact]
    public void ConfirmationRequest_ShouldHaveDefaultValues()
    {
        // Act
        var request = new ConfirmationRequest { Action = "Test", Description = "Test description" };

        // Assert
        request.Id.Should().NotBeNullOrEmpty();
        request.Type.Should().Be(ConfirmationType.Binary);
        request.RiskLevel.Should().Be(RiskLevel.Medium);
        request.Options.Should().BeEmpty();
        request.Context.Should().BeEmpty();
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        request.Timeout.Should().BeNull();
        request.DefaultOnTimeout.Should().BeNull();
    }

    [Fact]
    public void ConfirmationRequest_ShouldAllowCustomValues()
    {
        // Arrange
        var options = new[]
        {
            new ConfirmationOption
            {
                Id = "yes",
                Label = "Yes",
                IsDefault = true,
            },
            new ConfirmationOption
            {
                Id = "no",
                Label = "No",
                IsDangerous = true,
            },
        };

        // Act
        var request = new ConfirmationRequest
        {
            Id = "custom-id",
            Type = ConfirmationType.MultiChoice,
            Action = "Delete file",
            Description = "Delete important.txt?",
            RiskLevel = RiskLevel.High,
            Options = options,
            Context = new Dictionary<string, object> { ["filename"] = "important.txt" },
            Timeout = TimeSpan.FromMinutes(5),
            DefaultOnTimeout = "no",
        };

        // Assert
        request.Id.Should().Be("custom-id");
        request.Type.Should().Be(ConfirmationType.MultiChoice);
        request.Action.Should().Be("Delete file");
        request.Description.Should().Be("Delete important.txt?");
        request.RiskLevel.Should().Be(RiskLevel.High);
        request.Options.Should().HaveCount(2);
        request.Context.Should().ContainKey("filename");
        request.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        request.DefaultOnTimeout.Should().Be("no");
    }
}

public class ConfirmationResponseTests
{
    [Fact]
    public void ConfirmationResponse_ShouldHaveRequiredProperties()
    {
        // Act
        var response = new ConfirmationResponse { RequestId = "req-1", SelectedOption = "yes" };

        // Assert
        response.RequestId.Should().Be("req-1");
        response.SelectedOption.Should().Be("yes");
        response.RespondedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConfirmationResponse_ShouldAllowOptionalProperties()
    {
        // Act
        var response = new ConfirmationResponse
        {
            RequestId = "req-1",
            SelectedOption = "edit",
            FreeformInput = "user input",
            ModifiedContent = "modified content",
            RespondedBy = "admin",
            Reason = "looks good",
        };

        // Assert
        response.FreeformInput.Should().Be("user input");
        response.ModifiedContent.Should().Be("modified content");
        response.RespondedBy.Should().Be("admin");
        response.Reason.Should().Be("looks good");
    }
}

public class ConfirmationOptionTests
{
    [Fact]
    public void ConfirmationOption_ShouldHaveRequiredProperties()
    {
        // Act
        var option = new ConfirmationOption { Id = "approve", Label = "Approve" };

        // Assert
        option.Id.Should().Be("approve");
        option.Label.Should().Be("Approve");
        option.Description.Should().BeNull();
        option.IsDefault.Should().BeFalse();
        option.IsDangerous.Should().BeFalse();
    }

    [Fact]
    public void ConfirmationOption_ShouldAllowAllProperties()
    {
        // Act
        var option = new ConfirmationOption
        {
            Id = "delete",
            Label = "Delete",
            Description = "Permanently delete the file",
            IsDefault = false,
            IsDangerous = true,
        };

        // Assert
        option.Description.Should().Be("Permanently delete the file");
        option.IsDangerous.Should().BeTrue();
    }
}

public class RiskLevelTests
{
    [Theory]
    [InlineData(RiskLevel.Low, 0)]
    [InlineData(RiskLevel.Medium, 1)]
    [InlineData(RiskLevel.High, 2)]
    [InlineData(RiskLevel.Critical, 3)]
    public void RiskLevel_ShouldHaveCorrectValues(RiskLevel level, int expected)
    {
        // Assert
        ((int)level)
            .Should()
            .Be(expected);
    }
}

public class ConfirmationTypeTests
{
    [Fact]
    public void ConfirmationType_ShouldHaveAllExpectedValues()
    {
        // Assert
        Enum.GetValues<ConfirmationType>().Should().HaveCount(4);
        Enum.GetValues<ConfirmationType>()
            .Should()
            .Contain([
                ConfirmationType.Binary,
                ConfirmationType.MultiChoice,
                ConfirmationType.FreeformInput,
                ConfirmationType.Review,
            ]);
    }
}
