using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using InterviewQuiz.Openings.Application.Services;

namespace InterviewQuiz.Host.Hosting;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddInterviewQuizObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "interviewquiz-api";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(OpeningService.ActivitySource.Name);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
