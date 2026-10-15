namespace InterviewQuiz.Kernel.Exceptions;

/// <summary>
/// The request conflicts with a lockout or lifecycle rule. Maps to HTTP 409.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
