namespace InterviewQuiz.Delivery.Domain;

public static class LivePauseRules
{
    public const int MaxSinglePauseMinutes = 15;
    public const int MaxTotalPauseMinutes = 30;
    public const int ConnectedWindowSeconds = 15;
    public const int JoinWindowSeconds = 30;

    public static readonly TimeSpan MaxSinglePause = TimeSpan.FromMinutes(MaxSinglePauseMinutes);
    public static readonly TimeSpan MaxTotalPause = TimeSpan.FromMinutes(MaxTotalPauseMinutes);
    public static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(ConnectedWindowSeconds);
    public static readonly TimeSpan JoinWindow = TimeSpan.FromSeconds(JoinWindowSeconds);

    public static bool Exceeded(DateTimeOffset pausedAtUtc, int totalPausedSeconds, DateTimeOffset nowUtc)
    {
        var current = nowUtc - pausedAtUtc;
        if (current < TimeSpan.Zero)
        {
            current = TimeSpan.Zero;
        }

        if (current > MaxSinglePause)
        {
            return true;
        }

        var already = TimeSpan.FromSeconds(Math.Max(0, totalPausedSeconds));
        return already + current > MaxTotalPause;
    }

    public static bool IsConnected(DateTimeOffset? lastSeenUtc, DateTimeOffset nowUtc)
        => lastSeenUtc is { } seen && nowUtc - seen <= ConnectedWindow;

    public static bool JoinedRecently(DateTimeOffset? lastSeenUtc, DateTimeOffset nowUtc)
        => lastSeenUtc is { } seen && nowUtc - seen <= JoinWindow;
}
