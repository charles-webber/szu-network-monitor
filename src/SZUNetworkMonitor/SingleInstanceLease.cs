using System.Security.Principal;

namespace SZUNetworkMonitor;

// A Local mutex includes the current user's SID, so other Windows users are
// unaffected while duplicate tray processes for this user exit immediately.
internal sealed class SingleInstanceLease : IDisposable
{
    private Mutex? _mutex;

    private SingleInstanceLease(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceLease? TryAcquireForCurrentUser()
    {
        var userSid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(userSid))
        {
            userSid = Environment.UserName;
        }
        return TryAcquire($"Local\\SZUNetworkMonitor-{userSid}");
    }

    internal static SingleInstanceLease? TryAcquire(string mutexName)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstanceLease(mutex);
    }

    public void Dispose()
    {
        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
        {
            return;
        }

        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The process did not own the mutex, so disposing it is enough.
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
