using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsStartupRegistrationTests
{
    private static string Executable => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CouchPad.exe"));

    [Fact]
    public void Enables_and_disables_only_its_own_entry()
    {
        var store = new FakeStore();
        var registration = new WindowsStartupRegistration(store);
        registration.SetEnabled(true, Executable);
        Assert.Equal($"\"{Executable}\"", store.Value);
        registration.SetEnabled(true, Executable);
        Assert.Equal(1, store.Writes);
        registration.SetEnabled(false, Executable);
        Assert.Null(store.Value);
        Assert.Equal(1, store.Deletes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Foreign_entry_is_never_overwritten_or_removed(bool enabled)
    {
        var store = new FakeStore { Value = "foreign command" };
        Assert.Throws<InvalidOperationException>(() => new WindowsStartupRegistration(store).SetEnabled(enabled, Executable));
        Assert.Equal("foreign command", store.Value);
        Assert.Equal(0, store.Writes + store.Deletes);
    }

    [Fact]
    public void Failed_write_is_reported()
    {
        var store = new FakeStore { IgnoreWrites = true };
        Assert.Throws<IOException>(() => new WindowsStartupRegistration(store).SetEnabled(true, Executable));
    }

    [Fact]
    public void Rejects_relative_or_unrelated_executable()
    {
        var registration = new WindowsStartupRegistration(new FakeStore());
        Assert.Throws<ArgumentException>(() => registration.SetEnabled(true, "CouchPad.exe"));
        Assert.Throws<ArgumentException>(() => registration.SetEnabled(true, Path.Combine(Path.GetTempPath(), "other.exe")));
    }

    private sealed class FakeStore : IStartupRegistrationStore
    {
        public string? Value;
        public int Writes, Deletes;
        public bool IgnoreWrites;
        public string? Read() => Value;
        public void Write(string command) { Writes++; if (!IgnoreWrites) Value = command; }
        public void Delete() { Deletes++; Value = null; }
    }
}
