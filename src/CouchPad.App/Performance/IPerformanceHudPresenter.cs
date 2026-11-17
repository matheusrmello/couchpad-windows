using CouchPad.Core.Performance;

namespace CouchPad.App.Performance;

public interface IPerformanceHudPresenter
{
    bool IsVisible { get; }

    void ApplySettings(PerformanceHudSettings settings);

    void Show(PerformanceSnapshot snapshot, PerformanceHudSettings settings);

    void Update(PerformanceSnapshot snapshot, PerformanceHudSettings settings);

    void Hide();
}

public interface IPerformanceHudController
{
    bool Enabled { get; }

    bool IsVisible { get; }

    void ApplySettings(PerformanceHudSettings settings);

    void SetEnabled(bool enabled);

    void Toggle();

    void SetSessionActive(bool active);

    event EventHandler? Changed;
}
