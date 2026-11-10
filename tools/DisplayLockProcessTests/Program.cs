using System.Diagnostics;
using CouchPad.Platform.Windows;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();

if (args.Length == 4 && args[0] == "--owner")
{
    await using var held = await DisplayMutationLock.AcquireAsync(args[1]);
    await File.WriteAllTextAsync(args[2], "ready");
    if (args[3] == "--wait-for-kill") await Task.Delay(Timeout.InfiniteTimeSpan);
    else
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!File.Exists(args[3])) await Task.Delay(25, deadline.Token);
    }
    return;
}

if (args.Length != 0) throw new ArgumentException("Unexpected arguments.");
var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing apphost path.");
if (!string.Equals(Path.GetFileName(executable), "DisplayLockProcessTests.exe", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Run the generated apphost executable.");
var root = Path.Combine(Path.GetTempPath(), "couchpad-lock-process-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

async Task WaitForFileAsync(string file)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (!File.Exists(file)) await Task.Delay(25, deadline.Token);
}

async Task RunAsync(string name, bool killOwner)
{
    var lockPath = Path.Combine(root, name + ".lock");
    var ready = Path.Combine(root, name + ".ready");
    var release = Path.Combine(root, name + ".release");
    var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("--owner");
    info.ArgumentList.Add(lockPath);
    info.ArgumentList.Add(ready);
    info.ArgumentList.Add(killOwner ? "--wait-for-kill" : release);
    using var child = Process.Start(info) ?? throw new InvalidOperationException("Owner process failed to start.");
    try
    {
        await WaitForFileAsync(ready);
        using (var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
        {
            try
            {
                await using var unexpected = await DisplayMutationLock.AcquireAsync(lockPath, cancelled.Token);
                throw new InvalidOperationException("Acquired a lock held by another process.");
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
        }
        using var waitLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waiting = DisplayMutationLock.AcquireAsync(lockPath, waitLimit.Token).AsTask();
        await Task.Delay(200);
        if (waiting.IsCompleted) throw new InvalidOperationException("Waiter did not wait for the owner.");
        if (killOwner) child.Kill(entireProcessTree: false);
        else await File.WriteAllTextAsync(release, "release");
        await child.WaitForExitAsync(waitLimit.Token);
        await using var acquired = await waiting.WaitAsync(waitLimit.Token);
        Console.WriteLine(name + ": PASS");
    }
    finally
    {
        if (!child.HasExited) { child.Kill(entireProcessTree: false); await child.WaitForExitAsync(); }
    }
}

try
{
    await RunAsync("ORDERLY_RELEASE", false);
    await RunAsync("OWNER_DEATH", true);
}
finally
{
    foreach (var name in new[] { "ORDERLY_RELEASE", "OWNER_DEATH" })
        foreach (var suffix in new[] { ".lock", ".ready", ".release" })
            File.Delete(Path.Combine(root, name + suffix));
    Directory.Delete(root);
}
