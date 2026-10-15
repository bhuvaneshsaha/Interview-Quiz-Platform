using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InterviewQuiz.Host.Hosting;

public sealed class BearerSecurityDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Description =
            "Modular Monolith host. Employee login is email/password via POST /api/auth/login (JWT bearer). " +
            "Candidates exchange a magic-link invite at POST /api/auth/magic-link/consume. " +
            "Entra ID is not in this slice.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Employee access token from POST /api/auth/login or POST /api/auth/refresh. " +
                "Candidate access token from POST /api/auth/magic-link/consume."
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
