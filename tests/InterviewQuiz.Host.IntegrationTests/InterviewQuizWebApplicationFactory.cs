using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InterviewQuiz.Host.IntegrationTests;

public sealed class InterviewQuizWebApplicationFactory : WebApplicationFactory<Program>
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__InterviewQuiz");

    public static bool HasDatabase => !string.IsNullOrWhiteSpace(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            if (HasDatabase)
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:InterviewQuiz"] = ConnectionString
                });
            }
        });
    }
}
