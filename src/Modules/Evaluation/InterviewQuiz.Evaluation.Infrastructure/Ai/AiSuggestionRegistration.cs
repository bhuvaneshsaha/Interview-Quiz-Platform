using InterviewQuiz.Evaluation.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewQuiz.Evaluation.Infrastructure.Ai;

public static class AiSuggestionRegistration
{
    public static IServiceCollection AddAiSuggestions(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = AiSuggestionMode.Resolve(
            configuration["Ai:Provider"],
            configuration["Ai:BaseUrl"],
            configuration["Ai:Model"],
            configuration["Ai:ApiKey"]);

        if (mode == AiSuggestionMode.Fake)
        {
            services.AddSingleton<IAiSuggestionClient, FakeAiSuggestionClient>();
            return services;
        }

        if (mode == AiSuggestionMode.OpenAiCompatible)
        {
            services.AddHttpClient<IAiSuggestionClient, OpenAiCompatibleSuggestionClient>(client =>
            {
                var baseUrl = configuration["Ai:BaseUrl"]!.Trim().TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl);
                if (int.TryParse(configuration["Ai:TimeoutSeconds"], out var seconds) && seconds > 0)
                {
                    client.Timeout = TimeSpan.FromSeconds(Math.Min(seconds, 120));
                }
            });
            return services;
        }

        services.AddSingleton<IAiSuggestionClient, DisabledAiSuggestionClient>();
        return services;
    }
}
