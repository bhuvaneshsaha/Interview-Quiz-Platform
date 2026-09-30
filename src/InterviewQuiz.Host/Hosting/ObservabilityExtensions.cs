using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Catalog.Application.Services;
using InterviewQuiz.Delivery.Application.Services;
using InterviewQuiz.Evaluation.Application.Services;
using InterviewQuiz.Openings.Application.Services;
using InterviewQuiz.Search.Application.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
                    .AddSource(OpeningService.ActivitySource.Name)
                    .AddSource(QuizService.ActivitySource.Name)
                    .AddSource(TemplateService.ActivitySource.Name)
                    .AddSource(BankQuestionService.ActivitySource.Name)
                    .AddSource(FilterService.ActivitySource.Name)
                    .AddSource(MagicLinkService.ActivitySource.Name)
                    .AddSource(AssignmentService.ActivitySource.Name)
                    .AddSource(AttemptService.ActivitySource.Name);

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
