using InterviewQuiz.Access;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Catalog;
using InterviewQuiz.Delivery;
using InterviewQuiz.Evaluation;
using InterviewQuiz.Host.Hosting;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Openings.Infrastructure;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using InterviewQuiz.Search;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "InterviewQuiz.Host")
            .WriteTo.Console();
    });

    builder.Services.AddSingleton<IClock, UtcClock>();
    builder.Services.AddInterviewQuizObservability(builder.Configuration);

    builder.Services.AddAccessModule(builder.Configuration);
    builder.Services.AddOpeningsModule(builder.Configuration);
    builder.Services.AddCatalogModule();
    builder.Services.AddDeliveryModule();
    builder.Services.AddEvaluationModule();
    builder.Services.AddSearchModule();

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        };
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var useDevelopmentAuth = builder.Environment.IsDevelopment()
        || builder.Environment.IsEnvironment("Testing");

    if (useDevelopmentAuth)
    {
        builder.Services
            .AddAuthentication(DevelopmentTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentTestAuthHandler>(
                DevelopmentTestAuthHandler.SchemeName,
                _ => { });
    }
    else
    {
        builder.Services
            .AddAuthentication(UnconfiguredAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, UnconfiguredAuthenticationHandler>(
                UnconfiguredAuthenticationHandler.SchemeName,
                _ => { });
    }

    builder.Services.AddAuthorization();

    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<AccessDbContext>("access-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
        .AddDbContextCheck<OpeningsDbContext>("openings-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Interview Quiz API",
            Version = "v1",
            Description = "Modular Monolith host. Login is not implemented yet (Auth next). " +
                          "In Development/Testing send Authorization: Test {user}."
        });
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
        };
    });
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Interview Quiz API v1");
        });

        using var scope = app.Services.CreateScope();
        var accessDb = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var openingsDb = scope.ServiceProvider.GetRequiredService<OpeningsDbContext>();
        await accessDb.Database.MigrateAsync();
        await openingsDb.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentOpeningSeeder>()
            .SeedAsync();
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
