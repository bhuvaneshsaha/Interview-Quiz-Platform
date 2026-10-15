namespace InterviewQuiz.Delivery.Domain;

public enum LiveSessionState
{
    Created = 0,
    Lobby = 1,
    Running = 2,
    Paused = 3,
    Ended = 4,
    Submitted = 5
}
