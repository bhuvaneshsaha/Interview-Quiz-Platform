namespace InterviewQuiz.Kernel.Assignments;

/// <summary>
/// Wire messages for live sessions. Delivery owns state; Evaluation owns the attempt clock.
/// </summary>
public static class LiveSessionMessages
{
    public const string WaitingForHost = "Waiting for the host to start.";
    public const string NotLive = "Assignment is not live.";
    public const string DurationRequiredToStart = "Set a duration before starting.";
    public const string CandidateNotJoined = "Candidate has not joined.";
    public const string Paused = "Session is paused.";
    public const string NotRunning = "Session is not running.";
    public const string NotPaused = "Session is not paused.";
    public const string AlreadyStarted = "Session already started.";
    public const string Ended = "Session has ended.";
    public const string QuestionIndexInvalid = "Question index is invalid.";
}

public static class LiveEndReasons
{
    public const string Host = "host";
    public const string Timeout = "timeout";
    public const string PauseLimit = "pauseLimit";
    public const string Submitted = "submitted";
}

public static class LiveSessionActionNames
{
    public const string LinkIssued = "linkIssued";
    public const string CandidateJoined = "candidateJoined";
    public const string Started = "started";
    public const string Paused = "paused";
    public const string Resumed = "resumed";
    public const string Ended = "ended";
    public const string AutoEnded = "autoEnded";
    public const string Submitted = "submitted";
}
