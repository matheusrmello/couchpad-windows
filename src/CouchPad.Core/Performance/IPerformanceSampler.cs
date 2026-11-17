namespace CouchPad.Core.Performance;

public interface IPerformanceSampler
{
    Task<PerformanceSnapshot> SampleAsync(CancellationToken cancellationToken = default);
}
