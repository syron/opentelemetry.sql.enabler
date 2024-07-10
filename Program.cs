using System.Net.Quic;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "OtelMetricsEnabler";
var serviceVersion = "1.0.0";

builder.Logging.AddOpenTelemetry(logging => {
                                    logging.IncludeFormattedMessage = true;
                                    logging.IncludeScopes = true;
                                    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                                        .AddService(serviceName: serviceName, serviceVersion: serviceVersion, serviceInstanceId: serviceName));
                                });

builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => {
                    metrics
                        .AddMeter(GLOBALS.OTELMETRICSMETERNAME)
                        .AddPrometheusExporter()
                        .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService(serviceName: serviceName, serviceVersion: serviceVersion, serviceInstanceId: serviceName));
                })
                .UseOtlpExporter();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration[GLOBALS.OTELSQLCONNECTIONSTRING];
if (connectionString is null) throw new ArgumentException("No sql connection string found. Shutdown.");
builder.Services.AddSingleton(new SqlMetricsService(GLOBALS.OTELMETRICSMETERNAME, connectionString));

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("OtelWorker");
    q.AddJob<OtelWorker>(opts => opts.WithIdentity(jobKey));
    
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("OtelWorker-timely-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()) // todo: replace with config value
    );
});
builder.Services.AddQuartzHostedService(q => { 
    q.WaitForJobsToComplete = true;
    q.StartDelay = TimeSpan.Zero;
    });

var app = builder.Build();

app.MapPrometheusScrapingEndpoint(); // this can be used if otel endpoint is not available.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
