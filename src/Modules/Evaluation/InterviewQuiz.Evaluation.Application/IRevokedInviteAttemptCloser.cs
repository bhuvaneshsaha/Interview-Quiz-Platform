namespace InterviewQuiz.Evaluation.Application;

/// <summary>
/// Ends the in-progress attempt when a recruiter revokes the invite.
/// Expiry does not call this.
/// </summary>
public interface IRevokedInviteAttemptCloser
{
    Task CloseInProgressAsync(Guid assignmentId, CancellationToken cancellationToken);
}
