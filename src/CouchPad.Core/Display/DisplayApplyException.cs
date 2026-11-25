namespace CouchPad.Core.Display;

/// <summary>
/// Technical classification of a failed display apply. The message is for logs
/// only; the UI must map the code to localized text and never show these names.
/// </summary>
public enum DisplayApplyErrorCode
{
    UnsupportedMode,
    DisplayNotReady,
    ModeRejected,
    ModeVerificationFailed,
    ScaleFailed,
    TopologyVerificationFailed,
}

/// <summary>
/// Fail-closed display apply error. Carries the requested mode and the native
/// result so the coordinator and the UI can explain the failure without parsing
/// the technical message.
/// </summary>
public sealed class DisplayApplyException : InvalidOperationException
{
    public DisplayApplyException(
        DisplayApplyErrorCode code,
        string message,
        int? requestedWidth = null,
        int? requestedHeight = null,
        int? requestedRefreshHz = null,
        int? nativeResult = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        RequestedWidth = requestedWidth;
        RequestedHeight = requestedHeight;
        RequestedRefreshHz = requestedRefreshHz;
        NativeResult = nativeResult;
    }

    public DisplayApplyErrorCode Code { get; }

    public int? RequestedWidth { get; }

    public int? RequestedHeight { get; }

    public int? RequestedRefreshHz { get; }

    public int? NativeResult { get; }

    /// <summary>Requested mode when width, height and refresh are all present.</summary>
    public DisplayModeOption? RequestedMode =>
        RequestedWidth is { } width && RequestedHeight is { } height && RequestedRefreshHz is { } refresh
            ? new DisplayModeOption(width, height, refresh)
            : null;

    public static DisplayApplyException ForMode(
        DisplayApplyErrorCode code,
        DisplayModeOption mode,
        string message,
        int? nativeResult = null) =>
        new(code, message, mode.Width, mode.Height, mode.RefreshHz, nativeResult);
}
