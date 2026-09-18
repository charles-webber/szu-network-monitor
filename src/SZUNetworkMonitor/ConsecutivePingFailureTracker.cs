namespace SZUNetworkMonitor;

// A single lost ping is common on Wi-Fi and should not trigger a campus login.
// Only repeated, adjacent failures indicate that authentication is likely gone.
internal sealed class ConsecutivePingFailureTracker
{
    private readonly int _threshold;

    public ConsecutivePingFailureTracker(int threshold)
    {
        if (threshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        _threshold = threshold;
    }

    public int ConsecutiveFailures { get; private set; }

    public bool RecordFailure()
    {
        ConsecutiveFailures++;
        return ConsecutiveFailures >= _threshold;
    }

    public void RecordSuccess()
    {
        ConsecutiveFailures = 0;
    }
}
