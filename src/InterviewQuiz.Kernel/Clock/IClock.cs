namespace InterviewQuiz.Kernel.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
