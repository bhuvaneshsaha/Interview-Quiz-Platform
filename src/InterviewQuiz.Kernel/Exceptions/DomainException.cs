namespace InterviewQuiz.Kernel.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
