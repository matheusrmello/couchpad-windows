using System.Xml.Linq;
using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class RescueLogonTaskTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void Task_targets_only_the_requested_user_and_runs_rescue_without_elevation()
    {
        var executable = Path.Combine(Path.GetTempPath(), "CouchPad & Test", "CouchPad.Rescue.exe");
        const string sid = "S-1-5-21-123-456-789-1001";
        var xml = XDocument.Parse(RescueLogonTask.CreateXml(executable, sid));
        Assert.Equal(sid, (string?)xml.Descendants(Ns + "LogonTrigger").Single().Element(Ns + "UserId"));
        var principal = xml.Descendants(Ns + "Principal").Single();
        Assert.Equal(sid, (string?)principal.Element(Ns + "UserId"));
        Assert.Equal("InteractiveToken", (string?)principal.Element(Ns + "LogonType"));
        Assert.Equal("LeastPrivilege", (string?)principal.Element(Ns + "RunLevel"));
        Assert.Equal(executable, (string?)xml.Descendants(Ns + "Command").Single());
        Assert.Equal("--startup-recovery", (string?)xml.Descendants(Ns + "Arguments").Single());
        Assert.Equal("IgnoreNew", (string?)xml.Descendants(Ns + "MultipleInstancesPolicy").Single());
        Assert.Equal("PT0S", (string?)xml.Descendants(Ns + "ExecutionTimeLimit").Single());
    }

    [Theory]
    [InlineData("S-1-5-21-1</UserId>")]
    [InlineData("")]
    public void Refuses_invalid_user_identity(string sid) =>
        Assert.Throws<ArgumentException>(() => RescueLogonTask.CreateXml(Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), sid));

    [Fact]
    public void Refuses_unrelated_executable() =>
        Assert.Throws<ArgumentException>(() => RescueLogonTask.CreateXml(Path.Combine(Path.GetTempPath(), "Other.exe"), "S-1-5-21-1"));

    [Fact]
    public void Existing_task_is_not_overwritten()
    {
        var store = new FakeStore { Xml = RescueLogonTask.CreateXml(
            Path.Combine(Path.GetTempPath(), "other", "CouchPad.Rescue.exe"), "S-1-5-21-1") };
        Assert.Throws<InvalidOperationException>(() => new RescueLogonTaskRegistration(store).Create(
            Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1"));
        Assert.Equal(0, store.Creates);
        Assert.Throws<InvalidOperationException>(() => new RescueLogonTaskRegistration(store).Remove(
            Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1"));
        Assert.Equal(0, store.Deletes);
    }

    [Fact]
    public void Missing_task_is_created_once_and_checked()
    {
        var store = new FakeStore();
        new RescueLogonTaskRegistration(store).Create(Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1");
        Assert.Equal(1, store.Creates);
        Assert.Equal(2, store.Reads);
        Assert.NotNull(store.Xml);
    }

    [Fact]
    public void Altered_registered_task_is_reported()
    {
        var store = new FakeStore { Tamper = true };
        Assert.Throws<InvalidDataException>(() => new RescueLogonTaskRegistration(store).Create(
            Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1"));
    }

    [Fact]
    public void Reinstall_and_uninstall_are_idempotent()
    {
        var executable = Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe");
        var store = new FakeStore();
        var registration = new RescueLogonTaskRegistration(store);
        registration.Create(executable, "S-1-5-21-1");
        registration.Create(executable, "S-1-5-21-1");
        registration.Remove(executable, "S-1-5-21-1");
        registration.Remove(executable, "S-1-5-21-1");
        Assert.Equal(1, store.Creates);
        Assert.Equal(1, store.Deletes);
        Assert.Null(store.Xml);
    }

    [Fact]
    public void Uninstall_preserves_task_for_another_executable_and_removes_its_own()
    {
        const string sid = "S-1-5-21-1";
        var executable = Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe");
        var other = Path.Combine(Path.GetTempPath(), "other", "CouchPad.Rescue.exe");
        var store = new FakeStore { Xml = RescueLogonTask.CreateXml(other, sid) };
        var registration = new RescueLogonTaskRegistration(store);
        registration.Remove(executable, sid, preserveOtherInstallations: true);
        Assert.Equal(0, store.Deletes);
        Assert.Throws<InvalidOperationException>(() => registration.Remove(executable, sid));
        registration.Remove(other, sid, preserveOtherInstallations: true);
        Assert.Equal(1, store.Deletes);
    }

    [Fact]
    public void Uninstall_still_rejects_an_unexpected_definition_at_its_own_path()
    {
        const string sid = "S-1-5-21-1";
        var executable = Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe");
        var store = new FakeStore { Xml = RescueLogonTask.CreateXml(executable, sid), Tamper = true };
        Assert.Throws<InvalidOperationException>(() => new RescueLogonTaskRegistration(store)
            .Remove(executable, sid, preserveOtherInstallations: true));
        Assert.Equal(0, store.Deletes);
    }

    [Fact]
    public void Removal_must_be_confirmed()
    {
        var executable = Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe");
        var store = new FakeStore { IgnoreDelete = true };
        var registration = new RescueLogonTaskRegistration(store);
        registration.Create(executable, "S-1-5-21-1");
        Assert.Throws<IOException>(() => registration.Remove(executable, "S-1-5-21-1"));
    }

    private sealed class FakeStore : IRescueLogonTaskStore
    {
        public bool Tamper, IgnoreDelete;
        public int Creates, Reads, Deletes;
        public string? Xml;
        public string? ReadXml(string name)
        {
            Reads++;
            return Tamper ? Xml?.Replace("LeastPrivilege", "HighestAvailable") : Xml;
        }
        public void Create(string name, string xml, string sid) { Creates++; Xml = xml; }
        public void Delete(string name) { Deletes++; if (!IgnoreDelete) Xml = null; }
    }
}
