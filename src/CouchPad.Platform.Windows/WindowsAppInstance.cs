using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace CouchPad.Platform.Windows;

/// <summary>Owns the desktop instance for a profile and accepts only requests to show its window.</summary>
public sealed class WindowsAppInstance : IDisposable
{
    private const byte Show = 1;
    private const byte Ack = 2;
    private readonly string _pipeName;
    private readonly FileStream _owner;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;

    private WindowsAppInstance(string pipeName, FileStream owner) => (_pipeName, _owner) = (pipeName, owner);

    public static async Task<WindowsAppInstance?> AcquireOrForwardAsync(string profileRoot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(profileRoot);
        var lockPath = Path.Combine(Path.GetFullPath(profileRoot), "app-instance.lock");
        var pipeName = "CouchPad-app-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockPath.ToUpperInvariant())));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                return new WindowsAppInstance(pipeName, new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
            {
                // The owner may still be starting or may exit before its pipe is ready.
            }

            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            attempt.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(attempt.Token).ConfigureAwait(false);
                await pipe.WriteAsync(new byte[] { Show }, attempt.Token).ConfigureAwait(false);
                var response = new byte[1];
                if (await pipe.ReadAsync(response, attempt.Token).ConfigureAwait(false) == 1 && response[0] == Ack)
                    return null;
            }
            catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { }
            catch (IOException) { }
            await Task.Delay(50, deadline.Token).ConfigureAwait(false);
        }
    }

    public void Listen(Func<CancellationToken, Task> showWindow)
    {
        ArgumentNullException.ThrowIfNull(showWindow);
        if (_listener is not null) throw new InvalidOperationException("The activation listener is already running.");
        _listener = Task.Run(() => ListenAsync(showWindow, _stop.Token));
    }

    private async Task ListenAsync(Func<CancellationToken, Task> showWindow, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
            using var request = CancellationTokenSource.CreateLinkedTokenSource(stop);
            request.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                var command = new byte[1];
                if (await pipe.ReadAsync(command, request.Token).ConfigureAwait(false) != 1 || command[0] != Show) continue;
                await showWindow(request.Token).WaitAsync(request.Token).ConfigureAwait(false);
                await pipe.WriteAsync(new byte[] { Ack }, request.Token).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested) { }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener?.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally
        {
            _stop.Dispose();
            _owner.Dispose();
        }
    }
}
