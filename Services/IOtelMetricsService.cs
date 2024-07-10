public interface IOtelMetricsService
{
    public Dictionary<string, object> Get();
    public Task CollectMetricsAsync(CancellationToken cancellationToken);
}