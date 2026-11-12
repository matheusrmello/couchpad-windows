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
        var store = new FakeStore { Present = true };
        Assert.Throws<InvalidOperationException>(() => new RescueLogonTaskRegistration(store).Create(
            Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1"));
        Assert.Equal(0, store.Creates);
    }

    [Fact]
    public void Missing_task_is_created_once_and_checked()
    {
        var store = new FakeStore();
        new RescueLogonTaskRegistration(store).Create(Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1");
        Assert.Equal(1, store.Creates);
        Assert.Equal(1, store.Reads);
        Assert.True(store.Present);
    }

    [Fact]
    public void Altered_registered_task_is_reported()
    {
        var store = new FakeStore { Tamper = true };
        Assert.Throws<InvalidDataException>(() => new RescueLogonTaskRegistration(store).Create(
            Path.Combine(Path.GetTempPath(), "CouchPad.Rescue.exe"), "S-1-5-21-1"));
    }

    private sealed class FakeStore : IRescueLogonTaskStore
    {
        public bool Present, Tamper;
        public int Creates, Reads;
        private string? _xml;
        public bool Exists(string name) { Reads++; return Present; }
        public string? ReadXml(string name) => Tamper ? _xml?.Replace("LeastPrivilege", "HighestAvailable") : _xml;
        public void Create(string name, string xml, string sid) { Creates++; Present = true; _xml = xml; }
    }
}
