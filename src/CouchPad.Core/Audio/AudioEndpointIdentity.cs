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
    /// the normalized saved name must match exactly one active endpoint. Returns
    /// null when nothing matches or when the identity is ambiguous.
    /// </summary>
    public static AudioEndpointRef? Resolve(
        IEnumerable<AudioEndpointRef> endpoints, string? savedEndpointId, string? savedFriendlyName)
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

        if (NormalizeName(savedFriendlyName) is not { } token)
        {
            return null;
        }

        var candidates = active
            .Where(endpoint => string.Equals(NormalizeName(endpoint.Name), token, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
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
