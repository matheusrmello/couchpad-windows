namespace CouchPad.App.ViewModels;

public partial class MainWindowViewModel
{
    public string HardwareDiagnosisText
    {
        get
        {
            var suggestion = FindHardwareSuggestion();
            var tvReason = DisplayReasonKey(SelectedTvTarget, suggestion.Tv, tv: true);
            var monitorReason = DisplayReasonKey(SelectedInternalTarget, suggestion.Monitor, tv: false);
            var audioLine = DescribeAudio(out var audioReason, out var audioCandidates);
            var confidence = ConfidenceKey(tvReason, monitorReason, audioReason, audioCandidates);

            var lines = new List<string>();
            if (SelectedEnvironmentProfile is { } profile)
                lines.Add(string.Format(_localization["diagnosis.profile"], profile.DisplayName));
            lines.Add(DescribeDisplay(_localization["diagnosis.tv"], SelectedTvTarget, tvReason));
            lines.Add(DescribeDisplay(_localization["diagnosis.monitor"], SelectedInternalTarget, monitorReason));
            lines.Add(audioLine);
            lines.Add(string.Format(_localization["diagnosis.confidence"],
                _localization[$"diagnosis.confidence.{confidence}"]));
            lines.Add(_localization[SummaryKey(suggestion, confidence, audioCandidates)]);
            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string? DisplayReasonKey(DisplayTargetOption? selected, DisplayTargetOption? suggested, bool tv)
    {
        if (selected is null) return null;
        if (!selected.IsAvailable) return "diagnosis.reason.unavailable";
        if (suggested is not null && selected.Matches(suggested))
            return tv ? "diagnosis.reason.tv_auto" : "diagnosis.reason.monitor_auto";
        return "diagnosis.reason.manual";
    }

    private string DescribeDisplay(string label, DisplayTargetOption? target, string? reasonKey)
    {
        if (target is null)
            return string.Format(_localization["diagnosis.missing"], label);

        var name = string.IsNullOrWhiteSpace(target.FriendlyName) ? target.DevicePath : target.FriendlyName;
        var edid = target.EdidManufacturer is { } manufacturer && target.EdidProduct is { } product
            ? $"{manufacturer}/{product}"
            : "?";
        var transport = target.IsHdmi ? _localization["diagnosis.hdmi"] : _localization["diagnosis.not_hdmi"];
        var active = target.IsActive ? _localization["diagnosis.active"] : _localization["diagnosis.inactive"];
        var availability = target.IsAvailable ? _localization["diagnosis.available"] : _localization["diagnosis.unavailable"];
        var line = string.Format(_localization["diagnosis.display"], label, name, edid, transport, active, availability);
        return reasonKey is null ? line : line + " " + _localization[reasonKey];
    }

    private string DescribeAudio(out string? reasonKey, out int candidateCount)
    {
        var label = _localization["diagnosis.audio"];
        var audio = SelectedTvAudioEndpoint;
        candidateCount = FindAudioCandidates(SelectedTvTarget?.FriendlyName).Length;
        if (audio is null)
        {
            reasonKey = null;
            return string.Format(_localization["diagnosis.missing"], label);
        }

        var availability = audio.IsAvailable ? _localization["diagnosis.available"]
            : audio.CanAppearAfterDisplayChange ? _localization["diagnosis.later"]
            : _localization["diagnosis.unavailable"];
        reasonKey = AudioReasonKey(audio);
        var line = string.Format(_localization["diagnosis.audio_line"], label, audio.DisplayName, availability);
        return reasonKey is null ? line : line + " " + _localization[reasonKey];
    }

    private string? AudioReasonKey(AudioEndpointOption audio)
    {
        if (SelectedTvTarget?.FriendlyName is not { Length: > 0 } tvName)
            return "diagnosis.reason.audio_manual";
        if (audio.Name is not { } name)
            return "diagnosis.reason.audio_manual";
        if (name.Equals(tvName, StringComparison.OrdinalIgnoreCase))
            return "diagnosis.reason.audio_exact";
        if (name.StartsWith(tvName + " (", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(tvName + " -", StringComparison.OrdinalIgnoreCase))
            return "diagnosis.reason.audio_prefix";
        return "diagnosis.reason.audio_manual";
    }

    private string ConfidenceKey(string? tvReason, string? monitorReason, string? audioReason, int audioCandidates)
    {
        var complete = SelectedTvTarget is { IsAvailable: true } &&
            SelectedInternalTarget is { IsAvailable: true } &&
            SelectedTvAudioEndpoint is { } audio &&
            (audio.IsAvailable || audio.CanAppearAfterDisplayChange);
        if (!complete || audioCandidates > 1) return "low";
        if (tvReason == "diagnosis.reason.tv_auto" &&
            monitorReason == "diagnosis.reason.monitor_auto" &&
            audioReason == "diagnosis.reason.audio_exact")
            return "high";
        return "medium";
    }

    private string SummaryKey(HardwareSuggestion suggestion, string confidence, int audioCandidates)
    {
        if (audioCandidates > 1) return "diagnosis.summary.audio_ambiguous";
        if (suggestion.AvailableDisplayCount > 2) return "diagnosis.summary.multiple";
        if (confidence == "high") return "diagnosis.summary.simple";
        if (HasCompleteSelection) return "diagnosis.summary.complete";
        return "diagnosis.summary.incomplete";
    }
}
