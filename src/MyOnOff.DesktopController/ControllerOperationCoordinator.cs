namespace MyOnOff.DesktopController;

internal sealed class ControllerOperationCoordinator
{
    private readonly SemaphoreSlim _actionGate = new(1, 1);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _actionInProgress;

    public bool ShouldApplyRefreshResult => Volatile.Read(ref _actionInProgress) == 0;

    public bool TryBeginAction()
    {
        if (!_actionGate.Wait(0))
        {
            return false;
        }

        Volatile.Write(ref _actionInProgress, 1);
        return true;
    }

    public void EndAction()
    {
        Volatile.Write(ref _actionInProgress, 0);
        _actionGate.Release();
    }

    public bool TryBeginRefresh()
    {
        if (!ShouldApplyRefreshResult || !_refreshGate.Wait(0))
        {
            return false;
        }

        if (ShouldApplyRefreshResult)
        {
            return true;
        }

        _refreshGate.Release();
        return false;
    }

    public void EndRefresh() => _refreshGate.Release();
}
