namespace CouchPad.Platform.Windows;

/// <summary>Starts the app only after startup recovery completes successfully.</summary>
internal static class StartupLaunchSequence
{
    internal static async Task RunAsync(Func<Task> recover, Action launch)
    {
        ArgumentNullException.ThrowIfNull(recover);
        ArgumentNullException.ThrowIfNull(launch);
        await recover().ConfigureAwait(false);
        launch();
    }
}
