using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

public abstract class SyncFunctionBase
{
    private static readonly SemaphoreSlim _syncLock = new(1, 1);

    protected async Task<bool> ExecuteSync(Func<Task> syncAction, string triggerSource, string syncName)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromMilliseconds(100)))
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
            _syncLock.Release();
        }
    }
}
