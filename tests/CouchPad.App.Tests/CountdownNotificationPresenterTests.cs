using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;

namespace CouchPad.App.Tests;

public sealed class CountdownNotificationPresenterTests
{
    [Fact]
    public void Pending_shows_and_applying_dismisses_notification()
    {
        var shown = new List<TimeSpan>();
        var dismissed = 0;
        var presenter = new CountdownNotificationPresenter((duration, _, _) => shown.Add(duration), () => dismissed++);
        presenter.StateChanged(AutomationState.Pending, AutomationState.Pending, TimeSpan.FromSeconds(5), "TV", "{0}");
        presenter.StateChanged(AutomationState.Applying, AutomationState.Applying, TimeSpan.Zero, "TV", "{0}");
        Assert.Equal([TimeSpan.FromSeconds(5)], shown);
        Assert.Equal(2, dismissed);
    }

    [Fact]
    public void Stale_events_do_not_cancel_a_new_countdown()
    {
        var dismissed = 0;
        var presenter = new CountdownNotificationPresenter(null, () => dismissed++);
        presenter.StateChanged(AutomationState.WaitingForGamepad, AutomationState.Pending, TimeSpan.FromSeconds(2), "TV", "{0}");
        Assert.Equal(0, dismissed);
    }

    [Fact]
    public void Notification_failure_does_not_escape_transition()
    {
        var presenter = new CountdownNotificationPresenter((_, _, _) => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        presenter.StateChanged(AutomationState.Pending, AutomationState.Pending, TimeSpan.FromSeconds(2), "TV", "{0}");
    }
}
