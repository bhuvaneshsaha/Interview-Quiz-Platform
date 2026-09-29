using InterviewQuiz.Access.Application;
using InterviewQuiz.Access.Application.Services;
using InterviewQuiz.Access.Authentication;
using InterviewQuiz.Access.Authorization;
using InterviewQuiz.Access.Domain;
using InterviewQuiz.Access.Infrastructure;
using InterviewQuiz.Access.Infrastructure.Identity;
using InterviewQuiz.Access.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InterviewQuiz.Access;

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InterviewQuiz")
            ?? throw new InvalidOperationException(
                "Connection string 'InterviewQuiz' is not configured. Set ConnectionStrings__InterviewQuiz.");

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddDbContext<AccessDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "access");
                npgsql.EnableRetryOnFailure();
            });
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AccessDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IHostEnvironment>((bearer, jwtAccessor, environment) =>
            {
                var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
                bearer.MapInboundClaims = false;
                bearer.IncludeErrorDetails = local;
                bearer.RequireHttpsMetadata = !local;
                bearer.SaveToken = false;
                bearer.TokenValidationParameters = JwtTokenValidation.Create(jwtAccessor.Value);
            });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, HasPermissionHandler>();

        services.AddSingleton<IJwtAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityUserDirectory, IdentityUserDirectory>();
        services.AddScoped<IEffectivePermissionReader, EffectivePermissionReader>();
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<ICurrentUserQuery, CurrentUserQuery>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<DevelopmentAccessSeeder>();

        return services;
    }
}
