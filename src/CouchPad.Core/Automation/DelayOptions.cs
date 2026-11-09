namespace CouchPad.Core.Automation;

/// <summary>Atrasos oferecidos na UI, na ordem dos selects.</summary>
public static class DelayOptions
{
    public static IReadOnlyList<TimeSpan> Values { get; } = new[]
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
    };

    public static TimeSpan FromIndex(int index) =>
        index >= 0 && index < Values.Count ? Values[index] : TimeSpan.Zero;
}
