using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

public abstract class SyncFunctionBase
{
    // Instance-level semaphore so each function type has its own lock
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    protected async Task<bool> ExecuteSync(Func<Task> syncAction, string triggerSource, string syncName)
    {
        // Try to acquire the lock with a timeout
        bool lockAcquired = await _syncLock.WaitAsync(TimeSpan.FromMilliseconds(100));
        
        if (!lockAcquired)
        {
            Log.Warning("{SyncName} sync already in progress, skipping execution from {TriggerSource}", syncName, triggerSource);
            return false;
        }

        try
        {
            Log.Information("Starting {SyncName} sync from {TriggerSource} at {StartTime}", syncName, triggerSource, DateTime.UtcNow);
            await syncAction();
            Log.Information("Completed {SyncName} sync from {TriggerSource} at {EndTime}", syncName, triggerSource, DateTime.UtcNow);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{SyncName} sync failed from {TriggerSource}", syncName, triggerSource);
            throw;
        }
        finally
        {
            // Only release if we actually acquired the lock
            if (lockAcquired)
            {
                _syncLock.Release();
            }
        }
    }
}
