namespace SZUNetworkMonitor;

internal enum AuthenticationAttemptState
{
    Started,
    AlreadyRunning,
    CoolingDown
}

// Keeps authentication attempts serial and enforces the retry interval even
// when a connectivity check is manually requested.
internal sealed class AuthenticationAttemptGate
{
    private readonly object _syncRoot = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _failureCooldown;
    private bool _isRunning;
    private DateTimeOffset _nextAllowedAttempt = DateTimeOffset.MinValue;

    public AuthenticationAttemptGate(TimeProvider timeProvider, TimeSpan failureCooldown)
    {
        _timeProvider = timeProvider;
        _failureCooldown = failureCooldown;
    }

    public AuthenticationAttemptState TryBegin(out TimeSpan remainingCooldown)
    {
        lock (_syncRoot)
        {
            if (_isRunning)
            {
                remainingCooldown = TimeSpan.Zero;
                return AuthenticationAttemptState.AlreadyRunning;
            }

            var now = _timeProvider.GetUtcNow();
            if (now < _nextAllowedAttempt)
            {
                remainingCooldown = _nextAllowedAttempt - now;
                return AuthenticationAttemptState.CoolingDown;
            }

            _isRunning = true;
            remainingCooldown = TimeSpan.Zero;
            return AuthenticationAttemptState.Started;
        }
    }

    public void Complete(bool succeeded)
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            if (!succeeded)
            {
                _nextAllowedAttempt = _timeProvider.GetUtcNow().Add(_failureCooldown);
            }
        }
    }
}
