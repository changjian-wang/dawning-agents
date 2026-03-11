namespace Dawning.Agents.Tests.Logging;

using Dawning.Agents.Serilog;
using FluentAssertions;
using global::Serilog.Core;
using global::Serilog.Events;

public sealed class LogLevelControllerTests
{
    [Fact]
    public void TemporaryLevel_NestedScopes_DisposeOutOfOrder_ShouldKeepLatestActiveScope()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var controller = new LogLevelController(levelSwitch);

        var scope1 = controller.TemporaryLevel("Warning", TimeSpan.FromMinutes(1));
        controller.CurrentLevel.Should().Be("Warning");

        var scope2 = controller.TemporaryLevel("Error", TimeSpan.FromMinutes(1));
        controller.CurrentLevel.Should().Be("Error");

        scope1.Dispose();
        controller.CurrentLevel.Should().Be("Error");

        scope2.Dispose();
        controller.CurrentLevel.Should().Be("Information");
    }

    [Fact]
    public async Task TemporaryLevel_Timeout_ShouldRestoreToBaseLevel_WhenNoActiveScopes()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var controller = new LogLevelController(levelSwitch);

        using (controller.TemporaryLevel("Debug", TimeSpan.FromMilliseconds(50)))
        {
            controller.CurrentLevel.Should().Be("Debug");
            await Task.Delay(100);
        }

        controller.CurrentLevel.Should().Be("Information");
    }
}
