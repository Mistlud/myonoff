using MyOnOff.DesktopController;

namespace MyOnOff.Protocol.Tests;

public sealed class ControllerOperationCoordinatorTests
{
    [Fact]
    public void UserActionStartsWhileRefreshIsAlreadyRunning()
    {
        var operations = new ControllerOperationCoordinator();

        Assert.True(operations.TryBeginRefresh());
        Assert.True(operations.TryBeginAction());
        Assert.False(operations.ShouldApplyRefreshResult);

        operations.EndRefresh();
        operations.EndAction();
    }

    [Fact]
    public void UserActionBlocksEarlierRefreshResultAndRepeatedActions()
    {
        var operations = new ControllerOperationCoordinator();

        Assert.True(operations.TryBeginRefresh());
        Assert.True(operations.TryBeginAction());
        Assert.False(operations.TryBeginAction());
        Assert.False(operations.ShouldApplyRefreshResult);

        operations.EndRefresh();
        Assert.False(operations.TryBeginRefresh());

        operations.EndAction();
        Assert.True(operations.TryBeginRefresh());
        operations.EndRefresh();
    }
}
