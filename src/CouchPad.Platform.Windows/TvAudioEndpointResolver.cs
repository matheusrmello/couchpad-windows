using CouchPad.Core.Audio;

namespace CouchPad.Platform.Windows;

/// <summary>
/// Pure resolver over a captured Core Audio inventory. Never touches COM; the
/// identity rules live in <see cref="AudioEndpointIdentity"/> so they are
/// shared with the app without duplicating normalization.
/// </summary>
public static class TvAudioEndpointResolver
{
    /// <summary>
    /// Resolves the saved TV endpoint: active exact ID, then a unique active
    /// endpoint whose normalized friendly name matches, then the first token of
    /// the TV display name; null when unresolved or ambiguous.
    /// </summary>
    public static WindowsAudioProbe.Endpoint? Resolve(
        IEnumerable<WindowsAudioProbe.Endpoint> endpoints, string? savedEndpointId, string? savedFriendlyName,
        string? tvDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var source = endpoints as IReadOnlyList<WindowsAudioProbe.Endpoint> ?? endpoints.ToArray();
        var identity = AudioEndpointIdentity.Resolve(
            source.Select(endpoint => new AudioEndpointRef(endpoint.Id, endpoint.FriendlyName, endpoint.State == 1)),
            savedEndpointId, savedFriendlyName, tvDisplayName);
        if (identity is not { } match)
        {
            return null;
        }

        foreach (var endpoint in source)
        {
            if (string.Equals(endpoint.Id, match.Id, StringComparison.OrdinalIgnoreCase) && endpoint.State == 1)
            {
                return endpoint;
            }
        }

        return null;
    }

    /// <summary>First significant token of a friendly name; null when unusable.</summary>
    public static string? NormalizeName(string? friendlyName) => AudioEndpointIdentity.NormalizeName(friendlyName);
}
