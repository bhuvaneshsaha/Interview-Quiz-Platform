namespace InterviewQuiz.Kernel.Clock;

public sealed class UtcClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
