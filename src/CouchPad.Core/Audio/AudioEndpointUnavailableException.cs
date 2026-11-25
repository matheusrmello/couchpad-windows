namespace CouchPad.Core.Audio;

/// <summary>
/// The saved TV audio endpoint identity no longer matches a live endpoint and
/// could not be resolved by any stable identity. The message is for logs only;
/// the UI maps this type to localized text.
/// </summary>
public sealed class AudioEndpointUnavailableException : InvalidOperationException
{
    public AudioEndpointUnavailableException(
        string? savedName, string savedEndpointId, Exception? innerException = null)
        : base($"The saved TV audio endpoint '{savedName ?? "unknown"}' ({savedEndpointId}) is not available.",
            innerException)
    {
        SavedName = savedName;
        SavedEndpointId = savedEndpointId;
    }

    public string? SavedName { get; }

    public string SavedEndpointId { get; }
}
