namespace CouchPad.App.ViewModels;

public sealed record AudioEndpointOption(string Id, string? Name, bool IsAvailable = false, bool IsCurrent = false,
    bool CanAppearAfterDisplayChange = false, bool IsDisplayAudio = false)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} ({Id[^Math.Min(Id.Length, 9)..]})";
}
