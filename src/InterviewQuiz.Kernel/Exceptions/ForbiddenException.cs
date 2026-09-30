namespace InterviewQuiz.Kernel.Exceptions;

/// <summary>
/// Authenticated caller lacks the resource-level right (e.g. not the owner). Maps to HTTP 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
