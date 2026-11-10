using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class DisplaySessionOwnerTests
{
    [Fact]
    public void Only_one_owner_can_hold_the_same_journal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "couchpad-owner-" + Guid.NewGuid().ToString("N"));
        var journal = Path.Combine(directory, "session.json");
        try
        {
            using (DisplaySessionOwner.Acquire(journal))
                Assert.Throws<IOException>(() => DisplaySessionOwner.Acquire(journal));
            using var next = DisplaySessionOwner.Acquire(journal);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
