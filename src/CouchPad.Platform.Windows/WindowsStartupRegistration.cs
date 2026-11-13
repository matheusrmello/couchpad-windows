using Microsoft.Win32;

namespace CouchPad.Platform.Windows;

/// <summary>Manages only CouchPad's own per-user Windows startup entry.</summary>
public sealed class WindowsStartupRegistration
{
    private readonly IStartupRegistrationStore _store;
    public WindowsStartupRegistration() : this(new RegistryStartupRegistrationStore()) { }
    internal WindowsStartupRegistration(IStartupRegistrationStore store) => _store = store;

    private static string ExpectedCommand(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            !string.Equals(Path.GetFileName(executable), "CouchPad.exe", StringComparison.OrdinalIgnoreCase) || executable.Contains('"'))
            throw new ArgumentException("An absolute CouchPad.exe path is required.", nameof(executable));
        var rescue = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable))!, "CouchPad.Rescue.exe");
        return $"\"{rescue}\" --startup-and-launch";
    }

    public bool IsEnabled(string executable)
    {
        var expected = ExpectedCommand(executable);
        var current = _store.Read();
        if (current is not null && !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The existing startup entry is not owned by this installation.");
        return current is not null;
    }

    public void SetEnabled(bool enabled, string executable)
    {
        var expected = ExpectedCommand(executable);
        var current = _store.Read();
        if (current is not null && !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The existing startup entry is not owned by this installation.");
        if (enabled && current is null) _store.Write(expected);
        if (!enabled && current is not null) _store.Delete();
        var actual = _store.Read();
        if (enabled ? !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) : actual is not null)
            throw new IOException("Startup entry could not be verified.");
    }

    internal void RemoveIfOwned(string executable)
    {
        if (string.Equals(_store.Read(), ExpectedCommand(executable), StringComparison.OrdinalIgnoreCase))
            SetEnabled(false, executable);
    }
}

internal interface IStartupRegistrationStore
{
    string? Read();
    void Write(string command);
    void Delete();
}

internal sealed class RegistryStartupRegistrationStore : IStartupRegistrationStore
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CouchPad";
    public string? Read()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch { null => null, string text => text, _ => throw new InvalidDataException("Invalid startup entry type.") };
    }
    public void Write(string command)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true) ?? throw new IOException("Startup key unavailable.");
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }
    public void Delete()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
