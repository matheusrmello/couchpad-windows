using System.Xml.Linq;

namespace CouchPad.Platform.Windows;

/// <summary>Builds the per-user, interactive, least-privilege rescue task definition.</summary>
internal static class RescueLogonTask
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    internal static string CreateXml(string rescueExecutable, string userSid)
    {
        if (string.IsNullOrWhiteSpace(rescueExecutable) || !Path.IsPathFullyQualified(rescueExecutable) ||
            !string.Equals(Path.GetFileName(rescueExecutable), "CouchPad.Rescue.exe", StringComparison.OrdinalIgnoreCase) || rescueExecutable.Contains('"'))
            throw new ArgumentException("An absolute rescue executable path is required.", nameof(rescueExecutable));
        if (string.IsNullOrWhiteSpace(userSid) || !System.Text.RegularExpressions.Regex.IsMatch(userSid, @"^S-1-(?:\d+-)*\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("A user SID is required.", nameof(userSid));
        var path = Path.GetFullPath(rescueExecutable);
        var task = new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", "CouchPad session recovery")),
            new XElement(Ns + "Triggers", new XElement(Ns + "LogonTrigger", new XElement(Ns + "Enabled", "true"), new XElement(Ns + "UserId", userSid))),
            new XElement(Ns + "Principals", new XElement(Ns + "Principal", new XAttribute("id", "User"),
                new XElement(Ns + "UserId", userSid), new XElement(Ns + "LogonType", "InteractiveToken"), new XElement(Ns + "RunLevel", "LeastPrivilege"))),
            new XElement(Ns + "Settings", new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", "false"), new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                new XElement(Ns + "ExecutionTimeLimit", "PT0S"), new XElement(Ns + "Enabled", "true")),
            new XElement(Ns + "Actions", new XAttribute("Context", "User"), new XElement(Ns + "Exec",
                new XElement(Ns + "Command", path), new XElement(Ns + "Arguments", "--startup-recovery"),
                new XElement(Ns + "WorkingDirectory", Path.GetDirectoryName(path)!))));
        return new XDocument(new XDeclaration("1.0", "utf-16", null), task).ToString();
    }
}

/// <summary>Creates only a new task; never overwrites an existing task.</summary>
internal sealed class RescueLogonTaskRegistration(IRescueLogonTaskStore store)
{
    internal void Create(string executable, string sid)
    {
        var xml = RescueLogonTask.CreateXml(executable, sid);
        var name = "CouchPad-Rescue-" + sid;
        if (store.Exists(name)) throw new InvalidOperationException("A rescue task already exists.");
        store.Create(name, xml, sid);
        var registered = store.ReadXml(name) ?? throw new IOException("Rescue task registration could not be verified.");
        if (!Matches(registered, executable, sid)) throw new InvalidDataException("Registered rescue task differs from the intended task.");
    }

    private static bool Matches(string xml, string executable, string sid)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var root = XDocument.Parse(xml).Root ?? throw new InvalidDataException("Task definition missing.");
        var actions = root.Element(ns + "Actions")?.Elements().ToArray();
        var triggers = root.Element(ns + "Triggers")?.Elements().ToArray();
        var principals = root.Element(ns + "Principals")?.Elements().ToArray();
        if (actions?.Length != 1 || triggers?.Length != 1 || principals?.Length != 1) return false;
        var action = actions[0]; var trigger = triggers[0]; var principal = principals[0];
        var settings = root.Element(ns + "Settings");
        return action.Name == ns + "Exec" && trigger.Name == ns + "LogonTrigger" && principal.Name == ns + "Principal" &&
            string.Equals((string?)action.Element(ns + "Command"), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase) &&
            (string?)action.Element(ns + "Arguments") == "--startup-recovery" &&
            string.Equals((string?)action.Element(ns + "WorkingDirectory"), Path.GetDirectoryName(Path.GetFullPath(executable)), StringComparison.OrdinalIgnoreCase) &&
            (string?)trigger.Element(ns + "UserId") == sid && (string?)principal.Element(ns + "UserId") == sid &&
            (string?)trigger.Element(ns + "Enabled") == "true" && (string?)settings?.Element(ns + "Enabled") == "true" &&
            (string?)principal.Element(ns + "LogonType") == "InteractiveToken" && (string?)principal.Element(ns + "RunLevel") == "LeastPrivilege" &&
            (string?)settings?.Element(ns + "MultipleInstancesPolicy") == "IgnoreNew" &&
            (string?)settings?.Element(ns + "ExecutionTimeLimit") == "PT0S";
    }
}

internal interface IRescueLogonTaskStore
{
    bool Exists(string name);
    string? ReadXml(string name);
    void Create(string name, string xml, string sid);
}

/// <summary>Task Scheduler COM backend. Registration is explicit and never overwrites a task.</summary>
internal sealed class WindowsRescueLogonTaskStore : IRescueLogonTaskStore
{
    public bool Exists(string name) => WithFolder(folder =>
    {
        object? task = null;
        try { task = folder.GetTask(name); return true; }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80070002) { return false; }
        finally { Release(task); }
    });

    public string? ReadXml(string name) => WithFolder(folder =>
    {
        object? task = null;
        try { task = folder.GetTask(name); return (string)((dynamic)task).Xml; }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80070002) { return null; }
        finally { Release(task); }
    });

    public void Create(string name, string xml, string sid) => WithFolder<object?>(folder =>
    {
        // TASK_CREATE (2), TASK_LOGON_INTERACTIVE_TOKEN (3). No password and no overwrite.
        object? task = null;
        try { task = folder.RegisterTask(name, xml, 2, sid, null, 3, null); return null; }
        finally { Release(task); }
    });

    private static T WithFolder<T>(Func<dynamic, T> action)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        object? serviceObject = null;
        object? folderObject = null;
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Task Scheduler is unavailable.");
            serviceObject = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Task Scheduler could not start.");
            dynamic service = serviceObject;
            service.Connect();
            folderObject = service.GetFolder("\\");
            return action(folderObject);
        }
        finally { Release(folderObject); Release(serviceObject); }
    }

    private static void Release(object? value)
    {
        if (OperatingSystem.IsWindows() && value is not null && System.Runtime.InteropServices.Marshal.IsComObject(value))
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
    }
}
