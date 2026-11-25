using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class WindowsAudioProbeTests
{
    [Fact]
    public void Exact_name_selects_only_one_active_match()
    {
        var expected = new WindowsAudioProbe.Endpoint("active", 1, "SAMSUNG");
        var inactive = new WindowsAudioProbe.Endpoint("inactive", 4, "Samsung");
        Assert.Equal(expected, WindowsAudioProbe.SelectActiveExactName([expected, inactive], "samsung"));
    }

    [Fact]
    public void Missing_or_ambiguous_name_is_refused()
    {
        var first = new WindowsAudioProbe.Endpoint("first", 1, "SAMSUNG");
        var second = new WindowsAudioProbe.Endpoint("second", 1, "SAMSUNG");
        Assert.Throws<InvalidOperationException>(() => WindowsAudioProbe.SelectActiveExactName([first, second], "SAMSUNG"));
        Assert.Throws<InvalidOperationException>(() => WindowsAudioProbe.SelectActiveExactName([first], "OTHER"));
    }

    [Fact]
    public void Selected_endpoint_id_must_be_active_and_unique()
    {
        var selected = new WindowsAudioProbe.Endpoint("ID", 1, "Samsung");
        var other = new WindowsAudioProbe.Endpoint("other", 1, "Samsung");
        Assert.Equal(selected, WindowsAudioProbe.SelectActiveById([selected, other], "id"));
        Assert.Throws<InvalidOperationException>(() => WindowsAudioProbe.SelectActiveById([selected with { State = 4 }], "ID"));
        Assert.Throws<InvalidOperationException>(() => WindowsAudioProbe.SelectActiveById([selected, selected], "ID"));
        Assert.Throws<InvalidOperationException>(() => WindowsAudioProbe.SelectActiveById([other], "ID"));
    }

    [Fact]
    public void Stale_id_resolves_to_the_active_endpoint_with_the_saved_name()
    {
        const string savedName = "SAMSUNG (3- HD Audio Driver for Display Audio)";
        var stale = new WindowsAudioProbe.Endpoint("stale", 4, savedName);
        var live = new WindowsAudioProbe.Endpoint("live", 1, "SAMSUNG -2 (2- HD Audio Driver for Display Audio)");
        var monitor = new WindowsAudioProbe.Endpoint("monitor", 1, "AOC");

        Assert.Equal(live, TvAudioEndpointResolver.Resolve([stale, live, monitor], "stale", savedName));
        Assert.Equal(live, TvAudioEndpointResolver.Resolve([stale, live, monitor], null, savedName));
        Assert.Null(TvAudioEndpointResolver.Resolve([stale, monitor], "stale", savedName));
        Assert.Null(TvAudioEndpointResolver.Resolve([stale], "stale", null));
    }

    [Fact]
    public void Resolver_uses_the_ui_normalization_helper()
    {
        Assert.Equal("SAMSUNG", TvAudioEndpointResolver.NormalizeName(
            "SAMSUNG -2 (2- HD Audio Driver for Display Audio)"));
    }

    [Fact]
    public void Resolver_bootstraps_a_stale_endpoint_from_the_tv_display_name()
    {
        const string savedName = "SAMSUNG (3- HD Audio Driver for Display Audio)";
        var stale = new WindowsAudioProbe.Endpoint("{2ea8704a}", 4, savedName);
        var live = new WindowsAudioProbe.Endpoint("{45d7969c}", 1, "SAMSUNG -2 (2- HD Audio Driver for Display Audio)");
        var monitor = new WindowsAudioProbe.Endpoint("monitor", 1, "AOC 27G2");

        Assert.Equal(live, TvAudioEndpointResolver.Resolve(
            [stale, live, monitor], "{2ea8704a}", null, "SAMSUNG (SAM7167)"));
        Assert.Null(TvAudioEndpointResolver.Resolve([stale, monitor], "{2ea8704a}", null, "SAMSUNG (SAM7167)"));
        Assert.Null(TvAudioEndpointResolver.Resolve([stale, live, monitor], "{2ea8704a}", null));
    }
}
