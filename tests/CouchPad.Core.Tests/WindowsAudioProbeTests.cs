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
}
