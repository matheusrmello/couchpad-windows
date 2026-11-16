using CouchPad.App.Localization;
using CouchPad.App.ViewModels;
using CouchPad.Core.Automation;
using CouchPad.Core.Settings;

namespace CouchPad.App.Tests;

[Collection("Localization")]
public sealed class HardwareDiagnosisTests
{
    private static readonly DisplayTargetOption Tv =
        new("tv", 11596, 29031, "SAMSUNG") { IsAvailable = true, IsHdmi = true };
    private static readonly DisplayTargetOption Monitor =
        new("monitor", 58117, 9986, "AOC") { IsAvailable = true, IsActive = true };
    private static readonly AudioEndpointOption TvAudio =
        new("tv-audio", "SAMSUNG", IsAvailable: true, IsDisplayAudio: true);
    private static readonly AudioEndpointOption MonitorAudio =
        new("monitor-audio", "AOC", IsAvailable: true, IsCurrent: true);

    [Fact]
    public void Auto_suggestion_reports_high_confidence_and_reasons()
    {
        var store = Store();
        var vm = CreateViewModel(store, [Tv, Monitor], [TvAudio, MonitorAudio]);

        vm.SuggestHardwareCommand.Execute(null);

        Assert.Equal(Tv, vm.SelectedTvTarget);
        Assert.Equal(Monitor, vm.SelectedInternalTarget);
        Assert.Equal(TvAudio, vm.SelectedTvAudioEndpoint);
        Assert.Contains("Perfil: Padrão", vm.HardwareDiagnosisText);
        Assert.Contains("Confiança: alta.", vm.HardwareDiagnosisText);
        Assert.Contains("Motivo: única TV HDMI inativa com EDID.", vm.HardwareDiagnosisText);
        Assert.Contains("Motivo: único monitor ativo não-HDMI com EDID.", vm.HardwareDiagnosisText);
        Assert.Contains("Motivo: nome do endpoint igual ao nome da TV.", vm.HardwareDiagnosisText);
        Assert.Contains("Resumo: dois displays claros", vm.HardwareDiagnosisText);
    }

    [Fact]
    public void Audio_prefix_match_reports_medium_confidence()
    {
        var store = Store();
        var prefixed = new AudioEndpointOption("tv-prefix", "SAMSUNG - HDMI", IsAvailable: true, IsDisplayAudio: true);
        var vm = CreateViewModel(store, [Tv, Monitor], [prefixed, MonitorAudio]);

        vm.SuggestHardwareCommand.Execute(null);

        Assert.Equal(prefixed, vm.SelectedTvAudioEndpoint);
        Assert.Contains("Confiança: média.", vm.HardwareDiagnosisText);
        Assert.Contains("Motivo: nome do endpoint começa com o nome da TV.", vm.HardwareDiagnosisText);
    }

    [Fact]
    public void Multiple_displays_report_low_confidence_and_manual_choice()
    {
        var store = Store();
        var secondTv = new DisplayTargetOption("tv-2", 7789, 301, "LG TV") { IsAvailable = true, IsHdmi = true };
        var vm = CreateViewModel(store, [Tv, secondTv, Monitor], [TvAudio, MonitorAudio]);

        vm.SuggestHardwareCommand.Execute(null);

        Assert.Null(vm.SelectedTvTarget);
        Assert.Contains("TV: não selecionado.", vm.HardwareDiagnosisText);
        Assert.Contains("Confiança: baixa.", vm.HardwareDiagnosisText);
        Assert.Contains("Resumo: há mais de dois displays", vm.HardwareDiagnosisText);
    }

    [Fact]
    public void Unavailable_selected_target_reports_low_confidence()
    {
        var store = Store();
        var vm = CreateViewModel(store, [Tv, Monitor], [TvAudio, MonitorAudio]);
        vm.SelectedTvTarget = new DisplayTargetOption("tv-offline", 11596, 29031, "SAMSUNG");

        Assert.Contains("indisponível", vm.HardwareDiagnosisText);
        Assert.Contains("Motivo: o dispositivo não está disponível agora.", vm.HardwareDiagnosisText);
        Assert.Contains("Confiança: baixa.", vm.HardwareDiagnosisText);
    }

    private static FakeSettingsStore Store() => new()
    {
        Current = new AppSettings { Language = "pt-BR" },
    };

    private static MainWindowViewModel CreateViewModel(
        FakeSettingsStore store,
        IReadOnlyList<DisplayTargetOption> displays,
        IReadOnlyList<AudioEndpointOption> audio)
    {
        LocalizationService.Instance.Initialize(store);
        return new MainWindowViewModel(LocalizationService.Instance, store, new FakeJournal(),
            productionMode: true, configuredHardware: store.Current,
            discoverTargets: () => displays,
            discoverAudioEndpoints: () => audio,
            restartForHardware: () => { });
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
