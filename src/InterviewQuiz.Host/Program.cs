using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewQuiz.Access;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Seeding;
using InterviewQuiz.Catalog.Domain;
using InterviewQuiz.Catalog.Infrastructure;
using InterviewQuiz.Catalog.Infrastructure.Persistence;
using InterviewQuiz.Catalog.Infrastructure.Seeding;
using InterviewQuiz.Delivery;
using InterviewQuiz.Evaluation;
using InterviewQuiz.Host.Hosting;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Openings.Infrastructure;
using InterviewQuiz.Openings.Infrastructure.Persistence;
using InterviewQuiz.Openings.Infrastructure.Seeding;
using InterviewQuiz.Search;
using InterviewQuiz.Search.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(
        (context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "InterviewQuiz.Host")
                .WriteTo.Console();
        },
        preserveStaticLogger: true);

    builder.Services.AddSingleton<IClock, UtcClock>();
    builder.Services.AddInterviewQuizObservability(builder.Configuration);

    builder.Services.AddAccessModule(builder.Configuration);
    builder.Services.AddOpeningsModule(builder.Configuration);
    builder.Services.AddCatalogModule(builder.Configuration);
    builder.Services.AddDeliveryModule();
    builder.Services.AddEvaluationModule();
    builder.Services.AddSearchModule(builder.Configuration);

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new QuestionTypeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    });
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        };
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<AccessDbContext>("access-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
        .AddDbContextCheck<OpeningsDbContext>("openings-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
        .AddDbContextCheck<CatalogDbContext>("catalog-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
        .AddDbContextCheck<SearchDbContext>("search-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecurityDocumentTransformer>();
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Interview Quiz API",
            Version = "v1",
            Description =
                "Modular Monolith host. Employee login is email/password (JWT bearer). " +
                "Candidate magic-link and Entra ID are not in this slice."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token from POST /api/auth/login."
        });
        options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
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
        })
        .AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        })
        .AllowAnonymous();

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
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var searchDb = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        await accessDb.Database.MigrateAsync();
        await openingsDb.Database.MigrateAsync();
        await catalogDb.Database.MigrateAsync();
        await searchDb.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccessSeeder>()
            .SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentOpeningSeeder>()
            .SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentQuizSeeder>()
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
    // WebApplicationFactory builds Program more than once; disposing the static logger
    // between tests freezes Serilog for the next host.
    if (!string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Testing",
            StringComparison.OrdinalIgnoreCase))
    {
        await Log.CloseAndFlushAsync();
    }
}

public partial class Program;
