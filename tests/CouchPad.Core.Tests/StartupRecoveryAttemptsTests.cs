using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class StartupRecoveryAttemptsTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "CouchPadAttemptTests", Guid.NewGuid().ToString("N"), "attempts.json");

    [Fact]
    public void Three_attempts_are_allowed_then_blocked_until_clear()
    {
        var attempts = new StartupRecoveryAttempts(NewPath());
        var hash = new string('A', 64);
        attempts.Begin(hash);
        attempts.Begin(hash);
        attempts.Begin(hash);
        Assert.Throws<InvalidOperationException>(() => attempts.Begin(hash));
        attempts.Clear();
        attempts.Begin(hash);
    }

    [Fact]
    public void Different_baseline_has_its_own_attempt_budget()
    {
        var attempts = new StartupRecoveryAttempts(NewPath());
        for (var i = 0; i < 3; i++) attempts.Begin(new string('A', 64));
        attempts.Begin(new string('B', 64));
    }

    [Fact]
    public void Corrupt_attempt_record_blocks_automatic_recovery()
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{");
        Assert.Throws<InvalidDataException>(() => new StartupRecoveryAttempts(path).Begin(new string('A', 64)));
        Assert.Equal("{", File.ReadAllText(path));
    }
}
