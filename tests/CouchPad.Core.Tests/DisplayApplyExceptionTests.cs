using CouchPad.Core.Display;

namespace CouchPad.Core.Tests;

public sealed class DisplayApplyExceptionTests
{
    [Theory]
    [InlineData(DisplayApplyErrorCode.UnsupportedMode)]
    [InlineData(DisplayApplyErrorCode.DisplayNotReady)]
    [InlineData(DisplayApplyErrorCode.ModeRejected)]
    [InlineData(DisplayApplyErrorCode.ModeVerificationFailed)]
    [InlineData(DisplayApplyErrorCode.ScaleFailed)]
    [InlineData(DisplayApplyErrorCode.TopologyVerificationFailed)]
    public void Carries_code_native_result_and_requested_mode(DisplayApplyErrorCode code)
    {
        var error = new DisplayApplyException(code, "technical detail", 2560, 1440, 144, nativeResult: -2);

        Assert.Equal(code, error.Code);
        Assert.Equal(2560, error.RequestedWidth);
        Assert.Equal(1440, error.RequestedHeight);
        Assert.Equal(144, error.RequestedRefreshHz);
        Assert.Equal(-2, error.NativeResult);
        Assert.Equal(new DisplayModeOption(2560, 1440, 144), error.RequestedMode);
        Assert.IsAssignableFrom<InvalidOperationException>(error);
        Assert.Contains("technical detail", error.Message);
    }

    [Fact]
    public void ForMode_fills_every_requested_component()
    {
        var error = DisplayApplyException.ForMode(
            DisplayApplyErrorCode.ModeRejected, new DisplayModeOption(1280, 720, 60), "rejected", nativeResult: 87);

        Assert.Equal(DisplayApplyErrorCode.ModeRejected, error.Code);
        Assert.Equal(new DisplayModeOption(1280, 720, 60), error.RequestedMode);
        Assert.Equal(87, error.NativeResult);
    }

    [Fact]
    public void Requested_mode_is_null_without_a_complete_mode()
    {
        Assert.Null(new DisplayApplyException(DisplayApplyErrorCode.DisplayNotReady, "not ready").RequestedMode);
    }
}
