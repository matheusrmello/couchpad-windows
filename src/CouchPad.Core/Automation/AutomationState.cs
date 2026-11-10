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
    public AutomationStateChangedEventArgs(
        AutomationState state,
        string? error,
        bool recoveryUncertain = false)
    {
        State = state;
        Error = error;
        RecoveryUncertain = recoveryUncertain;
    }

    public AutomationState State { get; }

    public string? Error { get; }

    public bool RecoveryUncertain { get; }
}
