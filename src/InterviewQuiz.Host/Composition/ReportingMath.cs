namespace InterviewQuiz.Host.Composition;

/// <summary>
/// Pure Reporting v1 metrics. Pass uses the final score when review has settled every item,
/// and the auto-score ratio until that final score exists.
/// </summary>
public static class ReportingMath
{
    public const decimal PassThreshold = 0.70m;
    public const int DiscriminationMinimumN = 30;
    public const int DefaultWindowDays = 90;

    public readonly record struct FunnelCounts(
        int Assigned,
        int Appeared,
        int Submitted,
        int Completed,
        int PendingReview,
        decimal AppearedRate,
        decimal SubmittedRate,
        decimal CompletedRate);

    public readonly record struct FunnelAssignment(Guid AssignmentId, string CandidateEmail, string Status);

    public readonly record struct FunnelAttempt(bool Started, bool Submitted);

    public readonly record struct TimeTaken(int DurationSeconds, bool PauseTracked, string? Note);

    public readonly record struct ScoreSample(decimal AutoPointsAwarded, decimal AutoPointsAvailable);

    public static FunnelCounts Funnel(
        IEnumerable<FunnelAssignment> assignments,
        IReadOnlyDictionary<Guid, FunnelAttempt> attempts)
    {
        var rows = assignments.ToList();
        var emails = rows
            .Select(row => row.CandidateEmail)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var appeared = 0;
        var submitted = 0;
        var completed = 0;
        var pending = 0;
        foreach (var email in emails)
        {
            var matches = rows
                .Where(row => string.Equals(row.CandidateEmail, email, StringComparison.Ordinal))
                .ToList();
            if (matches.Any(row => attempts.TryGetValue(row.AssignmentId, out var attempt) && attempt.Started))
            {
                appeared++;
            }

            if (matches.Any(row => attempts.TryGetValue(row.AssignmentId, out var attempt) && attempt.Submitted))
            {
                submitted++;
            }

            if (matches.Any(row => row.Status == "completed"))
            {
                completed++;
            }

            if (matches.Any(row => row.Status == "pendingReview"))
            {
                pending++;
            }
        }

        return new FunnelCounts(
            emails.Count,
            appeared,
            submitted,
            completed,
            pending,
            Rate(appeared, emails.Count),
            Rate(submitted, emails.Count),
            Rate(completed, emails.Count));
    }

    public static decimal Rate(int numerator, int denominator)
        => denominator == 0
            ? 0m
            : decimal.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// When <paramref name="finalPointsAwarded"/> is set, pass is that total divided by
    /// <paramref name="totalPointsAvailable"/>. Until then, pass is the auto ratio and
    /// unsettled human points stay out of the denominator.
    /// </summary>
    public static bool? Passed(
        decimal autoPointsAwarded,
        decimal autoPointsAvailable,
        decimal? finalPointsAwarded = null,
        decimal totalPointsAvailable = 0m)
    {
        if (finalPointsAwarded is decimal final)
        {
            if (totalPointsAvailable <= 0)
            {
                return null;
            }

            return final / totalPointsAvailable >= PassThreshold;
        }

        if (autoPointsAvailable <= 0)
        {
            return null;
        }

        return autoPointsAwarded / autoPointsAvailable >= PassThreshold;
    }

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var mid = sorted.Length / 2;
        if (sorted.Length % 2 == 1)
        {
            return sorted[mid];
        }

        return (sorted[mid - 1] + sorted[mid]) / 2m;
    }

    public static int? Percentile(IReadOnlyList<int> values, double percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var rank = (int)Math.Ceiling(percentile * sorted.Length);
        if (rank < 1)
        {
            rank = 1;
        }

        if (rank > sorted.Length)
        {
            rank = sorted.Length;
        }

        return sorted[rank - 1];
    }

    public static int HistogramBucket(decimal autoPointsAwarded, decimal autoPointsAvailable)
    {
        if (autoPointsAvailable <= 0)
        {
            return -1;
        }

        var percent = autoPointsAwarded / autoPointsAvailable * 100m;
        if (percent >= 100m)
        {
            return 9;
        }

        if (percent <= 0m)
        {
            return 0;
        }

        var index = (int)(percent / 10m);
        return Math.Clamp(index, 0, 9);
    }

    public static string HistogramLabel(int bucket)
        => bucket switch
        {
            0 => "0–10",
            1 => "10–20",
            2 => "20–30",
            3 => "30–40",
            4 => "40–50",
            5 => "50–60",
            6 => "60–70",
            7 => "70–80",
            8 => "80–90",
            9 => "90–100",
            _ => ""
        };

    /// <summary>
    /// Async duration is submitted minus started. Live duration subtracts
    /// <paramref name="totalPausedSeconds"/> once and ignores per-pause events.
    /// </summary>
    public static TimeTaken Elapsed(
        bool live,
        DateTimeOffset startedAtUtc,
        DateTimeOffset submittedAtUtc,
        int totalPausedSeconds)
    {
        var wall = submittedAtUtc - startedAtUtc;
        if (wall < TimeSpan.Zero)
        {
            wall = TimeSpan.Zero;
        }

        if (!live)
        {
            return new TimeTaken(ToSeconds(wall), PauseTracked: true, Note: null);
        }

        var paused = TimeSpan.FromSeconds(Math.Max(0, totalPausedSeconds));
        var elapsed = wall - paused;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return new TimeTaken(ToSeconds(elapsed), PauseTracked: true, Note: null);
    }

    public static int DurationBucket(int durationSeconds)
    {
        if (durationSeconds < 5 * 60)
        {
            return 0;
        }

        if (durationSeconds < 15 * 60)
        {
            return 1;
        }

        if (durationSeconds < 30 * 60)
        {
            return 2;
        }

        if (durationSeconds < 60 * 60)
        {
            return 3;
        }

        return 4;
    }

    public static string DurationBucketLabel(int bucket)
        => bucket switch
        {
            0 => "Under 5 min",
            1 => "5–15 min",
            2 => "15–30 min",
            3 => "30–60 min",
            _ => "60 min or more"
        };

    /// <summary>
    /// Difficulty is the share of settled auto responses with full points.
    /// Discrimination is the top-quartile correct rate minus the bottom-quartile
    /// correct rate, and is hidden when fewer than 30 settled responses exist.
    /// </summary>
    public static (decimal Difficulty, decimal? Discrimination) QuestionStats(
        IReadOnlyList<(decimal AutoScore, bool FullPoints)> samples)
    {
        if (samples.Count == 0)
        {
            return (0m, null);
        }

        var correct = samples.Count(sample => sample.FullPoints);
        var difficulty = decimal.Round((decimal)correct / samples.Count, 4, MidpointRounding.AwayFromZero);
        if (samples.Count < DiscriminationMinimumN)
        {
            return (difficulty, null);
        }

        var ordered = samples
            .OrderBy(sample => sample.AutoScore)
            .ThenBy(sample => sample.FullPoints)
            .ToArray();
        var quartile = ordered.Length / 4;
        var bottomCorrect = ordered.Take(quartile).Count(sample => sample.FullPoints);
        var topCorrect = ordered.Skip(ordered.Length - quartile).Count(sample => sample.FullPoints);
        var bottomRate = (decimal)bottomCorrect / quartile;
        var topRate = (decimal)topCorrect / quartile;
        var discrimination = decimal.Round(topRate - bottomRate, 4, MidpointRounding.AwayFromZero);
        return (difficulty, discrimination);
    }

    public static decimal AutoScore(decimal awarded, decimal available)
        => available > 0 ? awarded / available : awarded;

    private static int ToSeconds(TimeSpan span)
        => (int)Math.Round(span.TotalSeconds, MidpointRounding.AwayFromZero);
}
