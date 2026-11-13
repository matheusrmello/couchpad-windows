using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public async Task Launch_waits_for_successful_recovery()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launched = false;
        var task = StartupLaunchSequence.RunAsync(() => gate.Task, () => launched = true);
        Assert.False(launched);
        gate.SetResult();
        await task;
        Assert.True(launched);
    }

    [Fact]
    public async Task Recovery_failure_blocks_launch()
    {
        var launched = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupLaunchSequence.RunAsync(
            () => Task.FromException(new InvalidOperationException()), () => launched = true));
        Assert.False(launched);
    }

    private static string Executable => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CouchPad.exe"));

    [Fact]
    public void Enables_and_disables_only_its_own_entry()
    {
        var store = new FakeStore();
        var registration = new WindowsStartupRegistration(store);
        registration.SetEnabled(true, Executable);
        Assert.Equal($"\"{Path.Combine(Path.GetDirectoryName(Executable)!, "CouchPad.Rescue.exe")}\" --startup-and-launch", store.Value);
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

    [Fact]
    public void Migration_registers_autostart_before_removing_legacy_and_leaves_rescue_task_alone()
    {
        const string sid = "S-1-5-21-1";
        var legacyStore = new FakeStore();
        var legacy = new WindowsStartupRegistration(legacyStore);
        legacy.SetEnabled(true, Executable);
        var tasks = new FakeTasks();
        var rescue = Path.Combine(Path.GetDirectoryName(Executable)!, "CouchPad.Rescue.exe");
        new RescueLogonTaskRegistration(tasks).Create(rescue, sid);
        var startup = new WindowsScheduledStartup(tasks, legacy, sid);
        tasks.FailCreate = true;
        Assert.Throws<IOException>(() => startup.ReadAndMigrate(Executable));
        Assert.NotNull(legacyStore.Value);
        tasks.FailCreate = false;
        Assert.True(startup.ReadAndMigrate(Executable));
        Assert.Null(legacyStore.Value);
        Assert.Contains("--startup-and-launch", tasks.Values["CouchPad-Autostart-" + sid]);
        Assert.True(startup.ReadAndMigrate(Executable));
        startup.SetEnabled(false, Executable);
        Assert.False(startup.ReadAndMigrate(Executable));
        Assert.Contains("--startup-recovery", Assert.Single(tasks.Values).Value);
    }

    private sealed class FakeTasks : IRescueLogonTaskStore
    {
        public Dictionary<string, string> Values { get; } = new();
        public bool FailCreate;
        public string? ReadXml(string name) => Values.GetValueOrDefault(name);
        public void Create(string name, string xml, string sid)
        {
            if (FailCreate) throw new IOException("Injected task registration failure.");
            Values.Add(name, xml);
        }
        public void Delete(string name) => Values.Remove(name);
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
