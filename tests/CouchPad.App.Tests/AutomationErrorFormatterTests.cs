using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;
using CouchPad.Core.Display;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

[Collection("Localization")]
public sealed class AutomationErrorFormatterTests
{
    private const string TechnicalDetail = "Changing the TV display mode failed (31).";
    private static readonly DisplayModeOption Mode = new(3840, 2160, 60);

    [Theory]
    [InlineData(DisplayApplyErrorCode.DisplayNotReady, "não ativou como monitor")]
    [InlineData(DisplayApplyErrorCode.ModeVerificationFailed, "não confirmou a mudança")]
    [InlineData(DisplayApplyErrorCode.ScaleFailed, "escala da TV")]
    [InlineData(DisplayApplyErrorCode.TopologyVerificationFailed, "não pôde ser confirmada")]
    public void Portuguese_codes_without_placeholders_use_their_own_message(
        DisplayApplyErrorCode code,
        string fragment)
    {
        var localization = Portuguese();

        var text = AutomationErrorFormatter.Format(localization, TechnicalDetail, code, null, null);

        Assert.NotNull(text);
        Assert.Contains(fragment, text);
        Assert.DoesNotContain("Falha na automação", text);
    }

    [Fact]
    public void Portuguese_unsupported_mode_lists_the_requested_mode()
    {
        var text = AutomationErrorFormatter.Format(Portuguese(), TechnicalDetail,
            DisplayApplyErrorCode.UnsupportedMode, Mode, null);

        Assert.NotNull(text);
        Assert.Contains("3840×2160 @ 60 Hz", text);
        Assert.Contains("Entrada de Sinal Plus", text);
    }

    [Fact]
    public void Portuguese_rejected_mode_lists_the_native_code()
    {
        var text = AutomationErrorFormatter.Format(Portuguese(), TechnicalDetail,
            DisplayApplyErrorCode.ModeRejected, Mode, -2);

        Assert.NotNull(text);
        Assert.Contains("3840×2160 @ 60 Hz", text);
        Assert.Contains("(código -2)", text);
    }

    [Theory]
    [InlineData(DisplayApplyErrorCode.UnsupportedMode)]
    [InlineData(DisplayApplyErrorCode.ModeRejected)]
    public void Missing_mode_falls_back_to_the_generic_message(DisplayApplyErrorCode code)
    {
        var localization = Portuguese();

        var text = AutomationErrorFormatter.Format(localization, TechnicalDetail, code, null, 31);

        Assert.Equal(string.Format(localization["errors.automation"], TechnicalDetail), text);
    }

    [Fact]
    public void Untyped_error_uses_the_generic_message()
    {
        var localization = Portuguese();

        var text = AutomationErrorFormatter.Format(localization, TechnicalDetail, null, Mode, null);

        Assert.Equal(string.Format(localization["errors.automation"], TechnicalDetail), text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_error_produces_no_text(string? error)
    {
        Assert.Null(AutomationErrorFormatter.Format(Portuguese(), error,
            DisplayApplyErrorCode.ScaleFailed, Mode, null));
    }

    [Fact]
    public void English_messages_use_the_same_placeholders()
    {
        var localization = English();

        var unsupported = AutomationErrorFormatter.Format(localization, TechnicalDetail,
            DisplayApplyErrorCode.UnsupportedMode, new DisplayModeOption(2560, 1440, 144), null);
        var rejected = AutomationErrorFormatter.Format(localization, TechnicalDetail,
            DisplayApplyErrorCode.ModeRejected, Mode, 31);
        var notReady = AutomationErrorFormatter.Format(localization, TechnicalDetail,
            DisplayApplyErrorCode.DisplayNotReady, null, null);

        Assert.Contains("does not advertise 2560×1440 @ 144 Hz", unsupported);
        Assert.Contains("Input Signal Plus", unsupported);
        Assert.Contains("rejected 3840×2160 @ 60 Hz (code 31)", rejected);
        Assert.Contains("did not activate as a monitor", notReady);
    }

    [Fact]
    public void Audio_endpoint_label_uses_the_friendly_name_without_the_guid_fragment()
    {
        var endpoint = new AudioEndpointOption("{0.0.0.00000000}.{b9168c9c-1234-4321-abcd-000000000000}",
            "SAMSUNG (3- HD Audio Driver for Display Audio)", IsAvailable: true);

        Assert.Equal("SAMSUNG (3- HD Audio Driver for Display Audio)", endpoint.DisplayName);
        Assert.DoesNotContain("b9168c9c", endpoint.DisplayName);
    }

    [Fact]
    public void Duplicate_endpoints_receive_a_readable_position_suffix()
    {
        var store = new FakeSettingsStore { Current = new AppSettings { Language = "pt-BR" } };
        var localization = LocalizationService.Instance;
        localization.Initialize(store);
        var name = "SAMSUNG (3- HD Audio Driver for Display Audio)";
        var first = new AudioEndpointOption("{0.0.0.00000000}.{b9168c9c-1111}", name, IsAvailable: true, IsDisplayAudio: true);
        var second = new AudioEndpointOption("{0.0.0.00000000}.{b9168c9c-2222}", name,
            CanAppearAfterDisplayChange: true, IsDisplayAudio: true);
        var other = new AudioEndpointOption("monitor-audio", "AOC", IsAvailable: true, IsCurrent: true);

        var vm = new MainWindowViewModel(localization, store, new FakeJournal(),
            discoverAudioEndpoints: () => [first, second, other]);

        Assert.Equal("AOC", other.DisplayName);
        Assert.Equal($"{name} (1 de 2)", vm.AudioEndpoints[1].DisplayName);
        Assert.Equal($"{name} (2 de 2)", vm.AudioEndpoints[2].DisplayName);
        Assert.All(vm.AudioEndpoints, endpoint => Assert.DoesNotContain("b9168c9c", endpoint.DisplayName));
    }

    private static LocalizationService Portuguese() => Initialize("pt-BR");

    private static LocalizationService English() => Initialize("en");

    private static LocalizationService Initialize(string language)
    {
        var localization = LocalizationService.Instance;
        localization.Initialize(new FakeSettingsStore { Current = new AppSettings { Language = language } });
        return localization;
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings Current { get; set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings) => Current = settings;
    }

    private sealed class FakeJournal : ISessionJournalStore
    {
        public SessionJournalEntry? Load() => null;

        public SessionJournalInspection Inspect() => new(SessionJournalStatus.Missing, null);

        public void Save(SessionJournalEntry entry)
        {
        }

        public void Clear()
        {
        }
    }
}
