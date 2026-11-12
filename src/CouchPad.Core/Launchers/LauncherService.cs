namespace CouchPad.Core.Launchers;

public enum LauncherKind { Steam, Hydra }

public sealed record LauncherSelection(LauncherKind Kind);

/// <summary>Only a process started by this service may be closed by it.</summary>
public interface ILauncherService
{
    Task StartAsync(LauncherSelection selection, CancellationToken cancellationToken = default);
    Task CloseOwnedAsync(CancellationToken cancellationToken = default);
}
