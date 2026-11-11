namespace CouchPad.App.ViewModels;

public sealed record AudioEndpointOption(string Id, string? Name)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : $"{Name} ({Id[^Math.Min(Id.Length, 9)..]})";
}
