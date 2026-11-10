namespace CouchPad.Platform.Windows;

/// <summary>Decision loop for a separate watchdog; all signals must be authenticated by its transport.</summary>
public static class RescueWatchdogLoop
{
    public enum Signal { Commit, Abort, Abandon }
    public enum Outcome { Released, Restored }

    public static async Task<Outcome> RunAsync(Task parentExited, Task timeout, Task<Signal> signal, Func<Task> restore)
    {
        ArgumentNullException.ThrowIfNull(parentExited);
        ArgumentNullException.ThrowIfNull(timeout);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(restore);
        await Task.WhenAny(parentExited, timeout, signal).ConfigureAwait(false);
        // A simultaneous parent death or timeout wins over a release signal.
        if (!parentExited.IsCompleted && !timeout.IsCompleted && signal.IsCompletedSuccessfully &&
            signal.Result is Signal.Commit or Signal.Abort)
            return Outcome.Released;
        await restore().ConfigureAwait(false);
        return Outcome.Restored;
    }
}
