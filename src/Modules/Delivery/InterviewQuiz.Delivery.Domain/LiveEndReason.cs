namespace InterviewQuiz.Delivery.Domain;

public enum LiveEndReason
{
    Host = 0,
    Timeout = 1,
    PauseLimit = 2,
    Submitted = 3
}
