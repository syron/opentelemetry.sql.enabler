using Quartz;
using System.Diagnostics.Metrics;

public class OtelWorker : IJob
{
    private readonly ILogger<OtelWorker> _logger;
    private readonly Meter _meter;
    private readonly SqlMetricsService _sqlMetricsService;

    public OtelWorker(ILogger<OtelWorker> logger, IMeterFactory meterFactory, SqlMetricsService sqlMetricsService)
    {
        _meter = meterFactory.Create(GLOBALS.OTELMETRICSMETERNAME);
        _logger = logger;
        _sqlMetricsService = sqlMetricsService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Started OtelWorker.Execute worker");

        await _sqlMetricsService.CollectMetricsAsync(context.CancellationToken);
    }
}       