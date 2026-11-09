namespace CouchPad.Core.Automation;

public enum AutomationState
{
    Idle,
    WaitingForGamepad,
    Pending,
    Applying,
    Active,
    Error,
    RescuePending,
}

public sealed class AutomationStateChangedEventArgs : EventArgs
{
    public AutomationStateChangedEventArgs(AutomationState state, string? error)
    {
        State = state;
        Error = error;
    }

    public AutomationState State { get; }

    public string? Error { get; }
}
