namespace InterviewQuiz.Kernel.Exceptions;

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string name, object key)
        : base($"{name} '{key}' was not found.")
    {
        EntityName = name;
        Key = key;
    }

    public string EntityName { get; }
    public object Key { get; }
}
