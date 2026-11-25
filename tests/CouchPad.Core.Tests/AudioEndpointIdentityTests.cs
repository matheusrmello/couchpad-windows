using CouchPad.Core.Audio;

namespace CouchPad.Core.Tests;

public sealed class AudioEndpointIdentityTests
{
    private const string SavedName = "SAMSUNG (3- HD Audio Driver for Display Audio)";
    private const string LiveName = "SAMSUNG -2 (2- HD Audio Driver for Display Audio)";

    [Fact]
    public void Normalization_ignores_windows_ordinals_and_the_driver_suffix()
    {
        Assert.Equal("SAMSUNG", AudioEndpointIdentity.NormalizeName(SavedName));
        Assert.Equal("SAMSUNG", AudioEndpointIdentity.NormalizeName(LiveName));
        Assert.Equal("SAMSUNG", AudioEndpointIdentity.NormalizeName("2- SAMSUNG (2- HD Audio Driver for Display Audio)"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_names_do_not_normalize(string? name)
    {
        Assert.Null(AudioEndpointIdentity.NormalizeName(name));
    }

    [Fact]
    public void Driver_only_name_normalizes_to_its_driver_token()
    {
        Assert.Equal("HD", AudioEndpointIdentity.NormalizeName("(3- HD Audio Driver for Display Audio)"));
    }

    [Fact]
    public void Active_exact_id_has_priority_over_the_saved_name()
    {
        var saved = new AudioEndpointRef("saved", SavedName, true);
        var other = new AudioEndpointRef("other", LiveName, true);

        Assert.Equal(saved, AudioEndpointIdentity.Resolve([other, saved], "saved", LiveName));
    }

    [Fact]
    public void Inactive_exact_id_falls_back_to_the_unique_active_name()
    {
        var stale = new AudioEndpointRef("stale", SavedName, false);
        var live = new AudioEndpointRef("live", LiveName, true);

        Assert.Equal(live, AudioEndpointIdentity.Resolve([stale, live], "stale", SavedName));
    }

    [Fact]
    public void Missing_id_falls_back_to_the_name_and_different_identity_is_refused()
    {
        var live = new AudioEndpointRef("live", LiveName, true);

        Assert.Equal(live, AudioEndpointIdentity.Resolve([live], null, "SAMSUNG"));
        Assert.Null(AudioEndpointIdentity.Resolve([live], "unknown", null));
        Assert.Null(AudioEndpointIdentity.Resolve([live], "unknown", "OTHER"));
    }

    [Fact]
    public void Ambiguous_name_tokens_are_not_resolved()
    {
        var first = new AudioEndpointRef("first", "SAMSUNG TV", true);
        var second = new AudioEndpointRef("second", LiveName, true);

        Assert.Null(AudioEndpointIdentity.Resolve([first, second], "stale", SavedName));
    }

    [Fact]
    public void Duplicate_active_exact_ids_are_ambiguous()
    {
        var first = new AudioEndpointRef("same", SavedName, true);
        var second = new AudioEndpointRef("same", SavedName, true);

        Assert.Null(AudioEndpointIdentity.Resolve([first, second], "same", SavedName));
    }
}
