using InterviewQuiz.Evaluation.Application.Ai;
using InterviewQuiz.Kernel.Ai;
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
            services.AddSingleton<IAiCompletionClient, FakeAiCompletionClient>();
            return services;
        }

        if (mode == AiSuggestionMode.OpenAiCompatible)
        {
            services.AddHttpClient<OpenAiChatCompletionClient>(client =>
            {
                var baseUrl = configuration["Ai:BaseUrl"]!.Trim().TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl);
                if (int.TryParse(configuration["Ai:TimeoutSeconds"], out var seconds) && seconds > 0)
                {
                    client.Timeout = TimeSpan.FromSeconds(Math.Min(seconds, 120));
                }
            });
            services.AddTransient<IAiCompletionClient>(sp => sp.GetRequiredService<OpenAiChatCompletionClient>());
            services.AddTransient<IAiSuggestionClient, OpenAiCompatibleSuggestionClient>();
            return services;
        }

        services.AddSingleton<IAiSuggestionClient, DisabledAiSuggestionClient>();
        services.AddSingleton<IAiCompletionClient, DisabledAiCompletionClient>();
        return services;
    }
}
