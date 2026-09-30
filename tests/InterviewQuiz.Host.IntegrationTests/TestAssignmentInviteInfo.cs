using InterviewQuiz.Kernel.Assignments;

namespace InterviewQuiz.Host.IntegrationTests;

/// <summary>
/// Test-only Delivery stand-in. Host Production does not register IAssignmentInviteInfo.
/// </summary>
public sealed class TestAssignmentInviteInfo : IAssignmentInviteInfo
{
    private readonly Dictionary<Guid, AssignmentInviteInfoDto> _items = [];
    private readonly object _gate = new();

    public void Set(AssignmentInviteInfoDto info)
    {
        lock (_gate)
        {
            _items[info.AssignmentId] = info;
        }
    }

    public void Remove(Guid assignmentId)
    {
        lock (_gate)
        {
            _items.Remove(assignmentId);
        }
    }

    public Task<AssignmentInviteInfoDto?> GetAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.TryGetValue(assignmentId, out var info) ? info : null);
        }
    }
}
