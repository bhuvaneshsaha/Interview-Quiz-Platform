using InterviewQuiz.Delivery.Application.Contracts;
using InterviewQuiz.Kernel.Pagination;

namespace InterviewQuiz.Delivery.Application;

public interface IAssignmentService
{
    Task<AssignmentResponse> CreateAsync(
        CreateAssignmentRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task<AssignmentResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<AssignmentSummaryResponse>> ListAsync(
        AssignmentListQuery query,
        CancellationToken cancellationToken);

    Task EnsureInvitableAsync(Guid id, CancellationToken cancellationToken);

    Task<AssignmentResponse> RecordInviteDeliveryAsync(
        Guid id,
        bool succeeded,
        string? error,
        CancellationToken cancellationToken);
}
