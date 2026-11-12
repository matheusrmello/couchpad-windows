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

/// <summary>Manages the task only when its definition belongs to this installation.</summary>
internal sealed class RescueLogonTaskRegistration(IRescueLogonTaskStore store)
{
    internal void Create(string executable, string sid)
    {
        var xml = RescueLogonTask.CreateXml(executable, sid);
        var name = "CouchPad-Rescue-" + sid;
        var existing = store.ReadXml(name);
        if (existing is not null)
        {
            if (!Matches(existing, executable, sid)) throw new InvalidOperationException("A different rescue task already exists.");
            return;
        }
        store.Create(name, xml, sid);
        var registered = store.ReadXml(name) ?? throw new IOException("Rescue task registration could not be verified.");
        if (!Matches(registered, executable, sid)) throw new InvalidDataException("Registered rescue task differs from the intended task.");
    }

    internal void Remove(string executable, string sid)
    {
        _ = RescueLogonTask.CreateXml(executable, sid);
        var name = "CouchPad-Rescue-" + sid;
        var existing = store.ReadXml(name);
        if (existing is null) return;
        if (!Matches(existing, executable, sid)) throw new InvalidOperationException("The rescue task belongs to a different configuration.");
        store.Delete(name);
        if (store.ReadXml(name) is not null) throw new IOException("Rescue task removal could not be verified.");
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
            IsSameUser((string?)trigger.Element(ns + "UserId"), sid) && IsSameUser((string?)principal.Element(ns + "UserId"), sid) &&
            ((bool?)trigger.Element(ns + "Enabled") ?? true) && ((bool?)settings?.Element(ns + "Enabled") ?? true) &&
            (string?)principal.Element(ns + "LogonType") == "InteractiveToken" && ((string?)principal.Element(ns + "RunLevel") ?? "LeastPrivilege") == "LeastPrivilege" &&
            (string?)settings?.Element(ns + "MultipleInstancesPolicy") == "IgnoreNew" &&
            !((bool?)settings?.Element(ns + "DisallowStartIfOnBatteries") ?? true) &&
            !((bool?)settings?.Element(ns + "StopIfGoingOnBatteries") ?? true) &&
            (string?)settings?.Element(ns + "ExecutionTimeLimit") == "PT0S";
    }

    private static bool IsSameUser(string? user, string sid)
    {
        if (string.Equals(user, sid, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(user) || !OperatingSystem.IsWindows()) return false;
        try
        {
            var identity = new System.Security.Principal.NTAccount(user);
            return identity.Translate(typeof(System.Security.Principal.SecurityIdentifier)).Value == sid;
        }
        catch (System.Security.Principal.IdentityNotMappedException) { return false; }
    }
}

internal interface IRescueLogonTaskStore
{
    string? ReadXml(string name);
    void Create(string name, string xml, string sid);
    void Delete(string name);
}

/// <summary>Task Scheduler COM backend. Registration is explicit and never overwrites a task.</summary>
internal sealed class WindowsRescueLogonTaskStore : IRescueLogonTaskStore
{
    public string? ReadXml(string name) => WithFolder(folder =>
    {
        object? task = null;
        try { task = folder.GetTask(name); return (string)((dynamic)task).Xml; }
        catch (FileNotFoundException) { return null; }
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

    public void Delete(string name) => WithFolder<object?>(folder =>
    {
        try { folder.DeleteTask(name, 0); }
        catch (FileNotFoundException) { }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80070002) { }
        return null;
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
