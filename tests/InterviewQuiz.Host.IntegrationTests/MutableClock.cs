using InterviewQuiz.Kernel.Clock;

namespace InterviewQuiz.Host.IntegrationTests;

public sealed class MutableClock : IClock
{
    private readonly object _gate = new();
    private DateTimeOffset? _frozen;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
            {
                return _frozen ?? DateTimeOffset.UtcNow;
            }
        }
    }

    public void Freeze(DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            _frozen = utcNow;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _frozen = (_frozen ?? DateTimeOffset.UtcNow).Add(by);
        }
    }

    public void Unfreeze()
    {
        lock (_gate)
        {
            _frozen = null;
        }
    }
}
