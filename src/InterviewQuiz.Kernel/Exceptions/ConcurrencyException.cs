namespace InterviewQuiz.Kernel.Exceptions;

public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }
}
