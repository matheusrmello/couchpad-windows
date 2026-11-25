namespace CouchPad.Core.Audio;

/// <summary>Minimal endpoint identity used to resolve a saved TV output.</summary>
public readonly record struct AudioEndpointRef(string Id, string? Name, bool IsActive);

/// <summary>
/// Pure identity rules for the saved TV audio endpoint. Core Audio can recreate
/// the endpoint ID after a driver reinstall or an EDID change, so the friendly
/// name is used only as a fallback and only when exactly one live endpoint
/// matches its first significant token.
/// </summary>
public static class AudioEndpointIdentity
{
    /// <summary>
    /// Reduces a friendly name to its first significant token, dropping the
    /// Windows ordinal prefix/suffix and the driver suffix:
    /// "SAMSUNG -2 (2- HD Audio Driver for Display Audio)" becomes "SAMSUNG".
    /// </summary>
    public static string? NormalizeName(string? friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            return null;
        }

        var text = friendlyName.Trim();
        var driverStart = text.IndexOf('(');
        if (driverStart > 0)
        {
            text = text[..driverStart].Trim();
        }

        text = TrimOrdinalPrefix(text);
        text = TrimOrdinalSuffix(text);
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = token.Trim('(', ')', '[', ']', '-', '.', ':', '_');
            if (cleaned.Length > 0 && cleaned.Any(char.IsLetterOrDigit))
            {
                return cleaned;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the TV endpoint by identity: an active exact ID wins; otherwise
    /// the normalized saved name must match exactly one active endpoint; when no
    /// saved name matches, the first token of the TV display name (for example
    /// "SAMSUNG (SAM7167)") is used as the last anchor. Returns null when nothing
    /// matches or when the identity is ambiguous, so an ambiguous identity is
    /// never written.
    /// </summary>
    public static AudioEndpointRef? Resolve(
        IEnumerable<AudioEndpointRef> endpoints, string? savedEndpointId, string? savedFriendlyName,
        string? tvDisplayName = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var active = endpoints.Where(endpoint => endpoint.IsActive).ToArray();

        if (!string.IsNullOrWhiteSpace(savedEndpointId))
        {
            var exact = active
                .Where(endpoint => string.Equals(endpoint.Id, savedEndpointId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (exact.Length == 1)
            {
                return exact[0];
            }

            if (exact.Length > 1)
            {
                return null;
            }
        }

        if (TryResolveByToken(active, NormalizeName(savedFriendlyName), out var byName, out var ambiguous))
        {
            return byName;
        }

        if (ambiguous)
        {
            return null;
        }

        return TryResolveByToken(active, NormalizeName(tvDisplayName), out var byDisplay, out _) ? byDisplay : null;
    }

    /// <summary>
    /// Unique active endpoint whose normalized name equals <paramref name="token"/>.
    /// A null token matches nothing; more than one match is reported as ambiguous.
    /// </summary>
    private static bool TryResolveByToken(
        IReadOnlyList<AudioEndpointRef> active, string? token, out AudioEndpointRef? match, out bool ambiguous)
    {
        match = null;
        ambiguous = false;
        if (token is null)
        {
            return false;
        }

        var candidates = active
            .Where(endpoint => string.Equals(NormalizeName(endpoint.Name), token, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length == 1)
        {
            match = candidates[0];
            return true;
        }

        ambiguous = candidates.Length > 1;
        return false;
    }

    private static string TrimOrdinalPrefix(string text)
    {
        var value = text;
        if (value.StartsWith('('))
        {
            value = value[1..].TrimStart();
        }

        var digits = 0;
        while (digits < value.Length && char.IsAsciiDigit(value[digits]))
        {
            digits++;
        }

        if (digits > 0 && digits < value.Length && value[digits] == '-')
        {
            value = value[(digits + 1)..].TrimStart();
        }

        return value;
    }

    private static string TrimOrdinalSuffix(string text)
    {
        var value = text.TrimEnd();
        var digits = 0;
        while (digits < value.Length && char.IsAsciiDigit(value[value.Length - 1 - digits]))
        {
            digits++;
        }

        if (digits > 0 && digits < value.Length && value[value.Length - digits - 1] == '-')
        {
            value = value[..(value.Length - digits - 1)].TrimEnd();
        }

        return value;
    }
}
