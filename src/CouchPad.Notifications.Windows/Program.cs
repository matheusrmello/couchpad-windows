using System.Globalization;
using System.Reflection;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

var diagnostic = args.Length == 5 && args[4] is "--diagnostic" or "--diagnostic-static";
var staticTest = diagnostic && args[4] == "--diagnostic-static";
if ((args.Length != 4 && !diagnostic) || !long.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deadlineMs) ||
    !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds) || durationSeconds <= 0)
    return;

var deadline = DateTimeOffset.FromUnixTimeMilliseconds(deadlineMs);
if (deadline <= DateTimeOffset.UtcNow) return;
var cancelled = Task.Run(Console.ReadLine);
var tag = Guid.NewGuid().ToString("N");
const string group = "countdown";
var manager = AppNotificationManager.Default;
var registered = false;
try
{
    manager.NotificationInvoked += (_, _) => { };
    var appName = typeof(Program).Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "CouchPad";
    manager.Register(appName, new Uri(Path.Combine(AppContext.BaseDirectory, "couchpad.ico")));
    registered = true;
    if (diagnostic) Console.WriteLine($"SETTING={manager.Setting}");
    var builder = new AppNotificationBuilder().AddText(args[2]);
    if (staticTest) builder.AddText(string.Format(CultureInfo.CurrentCulture, args[3], (int)Math.Ceiling(durationSeconds)));
    else builder.AddProgressBar(new AppNotificationProgressBar().BindValue().BindValueStringOverride().SetStatus(args[2]));
    var notification = builder.BuildNotification();
    notification.Tag = tag;
    notification.Group = group;
    notification.Expiration = deadline;
    notification.SuppressDisplay = false;
    uint sequence = 1;
    AppNotificationProgressData Data() {
        var seconds = Math.Max(0, (deadline - DateTimeOffset.UtcNow).TotalSeconds);
        return new AppNotificationProgressData(sequence++) {
            Value = Math.Clamp(1 - seconds / durationSeconds, 0, 1),
            ValueStringOverride = string.Format(CultureInfo.CurrentCulture, args[3], (int)Math.Ceiling(seconds)),
        };
    }
    if (!staticTest) notification.Progress = Data();
    if (cancelled.IsCompleted || deadline <= DateTimeOffset.UtcNow) return;
    manager.Show(notification);
    if (diagnostic) {
        Console.WriteLine("SHOWN");
        var all = await manager.GetAllAsync();
        Console.WriteLine($"PRESENT={all.Any(item => item.Tag == tag && item.Group == group)}");
    }
    while (deadline > DateTimeOffset.UtcNow)
    {
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (await Task.WhenAny(cancelled, Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1))) == cancelled)
            break;
        if (!staticTest && deadline > DateTimeOffset.UtcNow)
            await manager.UpdateAsync(Data(), tag, group);
    }
    await manager.RemoveByTagAndGroupAsync(tag, group);
    if (diagnostic) Console.WriteLine("REMOVED");
}
catch (Exception ex) { if (diagnostic) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; } }
finally { if (registered) { try { manager.Unregister(); } catch (Exception) { } } }
