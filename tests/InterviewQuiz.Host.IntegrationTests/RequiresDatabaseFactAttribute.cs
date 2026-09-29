namespace InterviewQuiz.Host.IntegrationTests;

public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public RequiresDatabaseFactAttribute()
    {
        if (!InterviewQuizWebApplicationFactory.HasDatabase)
        {
            Skip = "Set ConnectionStrings__InterviewQuiz to run API tests against PostgreSQL.";
        }
    }
}
