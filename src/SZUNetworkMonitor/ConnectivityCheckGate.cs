namespace SZUNetworkMonitor;

internal enum ConnectivityCheckState
{
    Started,
    AlreadyRunning,
    WaitingForNextInterval
}

// Serializes every connectivity check, including manual tray requests.  This
// prevents a fast successful check from being immediately repeated and keeps
// the normal cadence at one run per interval.
internal sealed class ConnectivityCheckGate
{
    private readonly object _syncRoot = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumInterval;
    private bool _isRunning;
    private DateTimeOffset _nextAllowedCheck = DateTimeOffset.MinValue;

    public ConnectivityCheckGate(TimeProvider timeProvider, TimeSpan minimumInterval)
    {
        _timeProvider = timeProvider;
        _minimumInterval = minimumInterval;
    }

    public ConnectivityCheckState TryBegin(out TimeSpan remainingInterval)
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                remainingInterval = TimeSpan.Zero;
                return ConnectivityCheckState.AlreadyRunning;
            }

            var now = _timeProvider.GetUtcNow();
            if (now < _nextAllowedCheck)
            {
                remainingInterval = _nextAllowedCheck - now;
                return ConnectivityCheckState.WaitingForNextInterval;
            }

            _isRunning = true;
            remainingInterval = TimeSpan.Zero;
            return ConnectivityCheckState.Started;
        }
    }

    public void Complete()
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            _nextAllowedCheck = _timeProvider.GetUtcNow().Add(_minimumInterval);
        }
    }
}
