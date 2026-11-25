namespace CouchPad.App.ViewModels;

public sealed record AudioEndpointOption(string Id, string? Name, bool IsAvailable = false, bool IsCurrent = false,
    bool CanAppearAfterDisplayChange = false, bool IsDisplayAudio = false, string? DuplicateLabel = null)
{
    /// <summary>Rótulo legível do endpoint: nome amigável, sem fragmento de GUID.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? Id
        : string.IsNullOrWhiteSpace(DuplicateLabel) ? Name : $"{Name} {DuplicateLabel}";
}
