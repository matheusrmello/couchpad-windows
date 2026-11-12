using CouchPad.Core.Automation;

namespace CouchPad.App.ViewModels;

internal sealed class CountdownNotificationPresenter(
    Action<TimeSpan, string, string>? show,
    Action? dismiss)
{
    public void StateChanged(AutomationState reported, AutomationState current, TimeSpan duration, string title, string remaining)
    {
        if (reported != current) return;
        try { dismiss?.Invoke(); } catch (Exception) { }
        if (reported == AutomationState.Pending && duration > TimeSpan.Zero)
        {
            try { show?.Invoke(duration, title, remaining); } catch (Exception) { }
        }
    }
}
