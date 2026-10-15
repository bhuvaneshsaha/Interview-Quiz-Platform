using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using InterviewQuiz.Catalog.Application;
using InterviewQuiz.Delivery.Application;
using InterviewQuiz.Evaluation.Application;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application;
using InterviewQuiz.Search.Application;

namespace InterviewQuiz.Host.Composition;

/// <summary>
/// Reporting v1. Composes Delivery and Evaluation public APIs in the host.
/// There is no reporting schema and no cross-schema join.
/// </summary>
public sealed class ReportingComposer
{
    public const string QuizOpeningMismatchMessage = "Quiz does not belong to that opening.";
    public const string QuizMissingMessage = "Quiz does not exist.";

    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Reports");

    private readonly IReportAssignmentReader _assignments;
    private readonly IReportAttemptReader _attempts;
    private readonly IOpeningService _openings;
    private readonly IQuizService _quizzes;
    private readonly IClock _clock;
    private readonly ILogger<ReportingComposer> _logger;

    public ReportingComposer(
        IReportAssignmentReader assignments,
        IReportAttemptReader attempts,
        IOpeningService openings,
        IQuizService quizzes,
        IClock clock,
        ILogger<ReportingComposer> logger)
    {
        _assignments = assignments;
        _attempts = attempts;
        _openings = openings;
        _quizzes = quizzes;
        _clock = clock;
        _logger = logger;
    }

    public Task<FunnelReportResponse> FunnelAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => FunnelAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public Task<ScoresReportResponse> ScoresAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => ScoresAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public Task<TimeReportResponse> TimeAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => TimeAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public Task<QuestionsReportResponse> QuestionsAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => QuestionsAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public Task<CandidatesReportResponse> CandidatesAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => CandidatesAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public Task<PendingReviewReportResponse> PendingReviewAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => PendingReviewAsync(criteria, page, pageSize, exportAll: false, cancellationToken);

    public async Task<byte[]> ExportAsync(string reportKey, JsonElement criteria, CancellationToken cancellationToken)
    {
        if (!ReportKeys.TryParseRoute(reportKey, out var key))
        {
            throw new EntityNotFoundException("Report", reportKey);
        }

        using var activity = ActivitySource.StartActivity("reports.export");
        activity?.SetTag("report.key", key);
        return key switch
        {
            ReportKeys.Funnel => ReportCsv.Write(FunnelHeaders, FunnelCsv(await FunnelRowsAsync(criteria, cancellationToken))),
            ReportKeys.Scores => ReportCsv.Write(ScoreHeaders, ScoreCsv((await ScoresAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items)),
            ReportKeys.Time => ReportCsv.Write(TimeHeaders, TimeCsv((await TimeAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items)),
            ReportKeys.Questions => ReportCsv.Write(QuestionHeaders, QuestionCsv((await QuestionsAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items)),
            ReportKeys.Candidates => ReportCsv.Write(CandidateHeaders, CandidateCsv((await CandidatesAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items)),
            ReportKeys.PendingReview => ReportCsv.Write(PendingHeaders, PendingCsv((await PendingReviewAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items)),
            _ => throw new EntityNotFoundException("Report", reportKey)
        };
    }

    private async Task<FunnelReportResponse> FunnelAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.Funnel, criteria, cancellationToken);
        var rows = BuildFunnelRows(scope);
        var request = new PageRequest(page, pageSize);
        return new FunnelReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            Totals = FunnelTotals(scope),
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<IReadOnlyList<FunnelReportRow>> FunnelRowsAsync(JsonElement criteria, CancellationToken cancellationToken)
        => (await FunnelAsync(criteria, 1, 1, exportAll: true, cancellationToken)).Items;

    private async Task<ScoresReportResponse> ScoresAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.Scores, criteria, cancellationToken);
        var rows = BuildScoreRows(scope);
        var points = rows.Select(row => row.AutoPointsAwarded).ToArray();
        var denominator = rows.Where(row => row.Passed is not null).ToArray();
        var passed = denominator.Count(row => row.Passed == true);
        var histogram = new int[10];
        foreach (var row in denominator)
        {
            var bucket = ReportingMath.HistogramBucket(row.AutoPointsAwarded, row.AutoPointsAvailable);
            if (bucket >= 0)
            {
                histogram[bucket]++;
            }
        }

        var request = new PageRequest(page, pageSize);
        return new ScoresReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            SubmittedCount = rows.Count,
            PassDenominator = denominator.Length,
            PassCount = passed,
            PassRate = ReportingMath.Rate(passed, denominator.Length),
            AverageAutoPoints = points.Length == 0 ? null : decimal.Round(points.Average(), 4, MidpointRounding.AwayFromZero),
            MinAutoPoints = points.Length == 0 ? null : points.Min(),
            MaxAutoPoints = points.Length == 0 ? null : points.Max(),
            MedianAutoPoints = ReportingMath.Median(points),
            Histogram = histogram.Select((count, index) => new ReportBucket
            {
                Label = ReportingMath.HistogramLabel(index),
                Count = count
            }).ToArray(),
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<TimeReportResponse> TimeAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.Time, criteria, cancellationToken);
        var rows = BuildTimeRows(scope);
        var durations = rows.Select(row => row.DurationSeconds).ToArray();
        var histogram = new int[5];
        foreach (var duration in durations)
        {
            histogram[ReportingMath.DurationBucket(duration)]++;
        }

        var request = new PageRequest(page, pageSize);
        return new TimeReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            P50Seconds = ReportingMath.Percentile(durations, 0.50),
            P90Seconds = ReportingMath.Percentile(durations, 0.90),
            Histogram = histogram.Select((count, index) => new ReportBucket
            {
                Label = ReportingMath.DurationBucketLabel(index),
                Count = count
            }).ToArray(),
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<QuestionsReportResponse> QuestionsAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.Questions, criteria, cancellationToken);
        var rows = BuildQuestionRows(scope);
        var request = new PageRequest(page, pageSize);
        return new QuestionsReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<CandidatesReportResponse> CandidatesAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.Candidates, criteria, cancellationToken);
        var rows = BuildCandidateRows(scope);
        var request = new PageRequest(page, pageSize);
        return new CandidatesReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<PendingReviewReportResponse> PendingReviewAsync(
        JsonElement criteria,
        int page,
        int pageSize,
        bool exportAll,
        CancellationToken cancellationToken)
    {
        var scope = await LoadAsync(ReportKeys.PendingReview, criteria, cancellationToken);
        var rows = BuildPendingRows(scope);
        var request = new PageRequest(page, pageSize);
        return new PendingReviewReportResponse
        {
            DefaultWindowApplied = scope.DefaultWindowApplied,
            AppliedCreatedFromUtc = scope.CreatedFrom,
            AppliedCreatedToUtc = scope.CreatedTo,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = rows.Count,
            Items = exportAll ? rows : Page(rows, request)
        };
    }

    private async Task<ReportScope> LoadAsync(string reportKey, JsonElement criteria, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("reports." + reportKey);
        var filter = ReportFilterParser.Parse(reportKey, criteria);
        activity?.SetTag("report.key", reportKey);
        var openingIds = filter.OpeningIds is null ? "" : string.Join(',', filter.OpeningIds);
        var quizIds = filter.QuizIds is null ? "" : string.Join(',', filter.QuizIds);
        activity?.SetTag("opening.ids", openingIds);
        activity?.SetTag("quiz.ids", quizIds);
        _logger.LogInformation(
            "Report {ReportKey} OpeningIds {OpeningIds} QuizIds {QuizIds}",
            reportKey,
            openingIds,
            quizIds);

        var created = filter.Created;
        var defaultWindow = false;
        if (!filter.Empty && !filter.HasCreatedConstraint)
        {
            created = new DateConstraint(
                _clock.UtcNow.AddDays(-ReportingMath.DefaultWindowDays),
                false,
                null,
                false,
                false);
            defaultWindow = true;
        }

        if (reportKey == ReportKeys.PendingReview && !filter.Empty)
        {
            if (filter.Statuses is null)
            {
                filter = WithStatuses(filter, ["pendingReview"]);
            }
            else if (!filter.Statuses.Contains("pendingReview", StringComparer.Ordinal))
            {
                filter = WithStatuses(filter, []);
            }
        }

        if (!filter.Empty && filter.OpeningIds is { Count: > 0 } openings && filter.QuizIds is { Count: > 0 } quizzes)
        {
            foreach (var quizId in quizzes)
            {
                try
                {
                    var quiz = await _quizzes.GetAsync(quizId, cancellationToken);
                    if (!openings.Contains(quiz.OpeningId))
                    {
                        throw new DomainException(QuizOpeningMismatchMessage);
                    }
                }
                catch (EntityNotFoundException)
                {
                    throw new DomainException(QuizMissingMessage);
                }
            }
        }

        if (filter.Empty || (filter.Statuses is { Count: 0 }))
        {
            return ReportScope.Empty(defaultWindow, created);
        }

        var assignments = await _assignments.ListAsync(new ReportAssignmentQuery
        {
            OpeningIds = filter.OpeningIds,
            QuizIds = filter.QuizIds,
            CreatedFromUtc = created.Lower,
            CreatedFromExclusive = created.LowerExclusive,
            CreatedToUtc = created.Upper,
            CreatedToExclusive = created.UpperExclusive,
            Statuses = filter.Statuses,
            Mode = filter.Mode,
            SnapshotId = filter.HasSnapshotConstraint ? filter.SnapshotId : null,
            QuizRowVersions = filter.QuizRowVersions
        }, cancellationToken);

        var attempts = await _attempts.ListForAssignmentsAsync(
            assignments.Select(row => row.AssignmentId).ToArray(),
            cancellationToken);
        var byAssignment = attempts
            .GroupBy(row => row.AssignmentId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ReportAttemptRow>)group.ToArray());

        var titles = new Dictionary<Guid, string>();
        foreach (var openingId in assignments.Select(row => row.OpeningId).Distinct())
        {
            try
            {
                var opening = await _openings.GetAsync(openingId, cancellationToken);
                titles[openingId] = opening.Title;
            }
            catch (EntityNotFoundException)
            {
                titles[openingId] = openingId.ToString();
            }
        }

        return new ReportScope(filter, assignments, byAssignment, titles, defaultWindow, created.Lower, created.Upper);
    }

    private static ReportFilterSet WithStatuses(ReportFilterSet filter, IReadOnlyCollection<string> statuses)
        => new()
        {
            Empty = filter.Empty || statuses.Count == 0,
            OpeningIds = filter.OpeningIds,
            QuizIds = filter.QuizIds,
            Created = filter.Created,
            HasCreatedConstraint = filter.HasCreatedConstraint,
            Submitted = filter.Submitted,
            HasSubmittedConstraint = filter.HasSubmittedConstraint,
            Statuses = statuses,
            Mode = filter.Mode,
            SnapshotId = filter.SnapshotId,
            HasSnapshotConstraint = filter.HasSnapshotConstraint,
            QuizRowVersions = filter.QuizRowVersions,
            Score = filter.Score,
            HasScoreConstraint = filter.HasScoreConstraint,
            PendingAgeHours = filter.PendingAgeHours,
            HasPendingAgeConstraint = filter.HasPendingAgeConstraint
        };

    private List<FunnelReportRow> BuildFunnelRows(ReportScope scope)
    {
        return scope.Assignments
            .GroupBy(row => (row.OpeningId, row.QuizId))
            .Select(group =>
            {
                var counts = Count(group, scope);
                var sample = group.First();
                return new FunnelReportRow
                {
                    OpeningId = sample.OpeningId,
                    OpeningTitle = Title(scope, sample.OpeningId),
                    QuizId = sample.QuizId,
                    QuizTitle = sample.SnapshotTitle,
                    Assigned = counts.Assigned,
                    Appeared = counts.Appeared,
                    Submitted = counts.Submitted,
                    Completed = counts.Completed,
                    PendingReview = counts.PendingReview,
                    AppearedRate = counts.AppearedRate,
                    SubmittedRate = counts.SubmittedRate,
                    CompletedRate = counts.CompletedRate
                };
            })
            .OrderBy(row => row.OpeningTitle, StringComparer.Ordinal)
            .ThenBy(row => row.QuizTitle, StringComparer.Ordinal)
            .ThenBy(row => row.QuizId)
            .ToList();
    }

    private static FunnelReportRow FunnelTotals(ReportScope scope)
    {
        var counts = Count(scope.Assignments, scope);
        return new FunnelReportRow
        {
            OpeningTitle = "All openings in scope",
            QuizTitle = "All quizzes in scope",
            Assigned = counts.Assigned,
            Appeared = counts.Appeared,
            Submitted = counts.Submitted,
            Completed = counts.Completed,
            PendingReview = counts.PendingReview,
            AppearedRate = counts.AppearedRate,
            SubmittedRate = counts.SubmittedRate,
            CompletedRate = counts.CompletedRate
        };
    }

    private static ReportingMath.FunnelCounts Count(IEnumerable<ReportAssignmentRow> rows, ReportScope scope)
    {
        var list = rows.ToList();
        var attempts = new Dictionary<Guid, ReportingMath.FunnelAttempt>();
        foreach (var row in list)
        {
            var relevant = RelevantAttempts(scope, row.AssignmentId);
            var submitted = relevant.Any(attempt => attempt.Status == "submitted");
            var restrict = scope.Filter.HasSubmittedConstraint || scope.Filter.HasScoreConstraint;
            attempts[row.AssignmentId] = new ReportingMath.FunnelAttempt(relevant.Count > 0, submitted);
            if (restrict && !submitted)
            {
                attempts[row.AssignmentId] = new ReportingMath.FunnelAttempt(false, false);
            }
        }

        var funnelRows = list.Select(row =>
        {
            var restrict = scope.Filter.HasSubmittedConstraint || scope.Filter.HasScoreConstraint;
            var submitted = attempts.TryGetValue(row.AssignmentId, out var attempt) && attempt.Submitted;
            var status = row.Status;
            if (restrict && !submitted)
            {
                status = "excluded";
            }

            return new ReportingMath.FunnelAssignment(row.AssignmentId, row.CandidateEmail, status);
        });
        return ReportingMath.Funnel(funnelRows, attempts);
    }

    private List<ScoreReportRow> BuildScoreRows(ReportScope scope)
    {
        var rows = new List<ScoreReportRow>();
        foreach (var assignment in scope.Assignments)
        {
            foreach (var attempt in SubmittedAttempts(scope, assignment.AssignmentId))
            {
                rows.Add(new ScoreReportRow
                {
                    AttemptId = attempt.AttemptId,
                    AssignmentId = assignment.AssignmentId,
                    OpeningId = assignment.OpeningId,
                    OpeningTitle = Title(scope, assignment.OpeningId),
                    QuizId = assignment.QuizId,
                    QuizTitle = assignment.SnapshotTitle,
                    CandidateEmail = assignment.CandidateEmail,
                    Mode = assignment.Mode,
                    Status = assignment.Status,
                    ResultStatus = attempt.ResultStatus,
                    AutoPointsAwarded = attempt.AutoPointsAwarded,
                    AutoPointsAvailable = attempt.AutoPointsAvailable,
                    TotalPointsAvailable = attempt.TotalPointsAvailable,
                    FinalPointsAwarded = attempt.FinalPointsAwarded,
                    ScoreBasis = attempt.FinalPointsAwarded is null ? "auto" : "final",
                    UnsettledCount = attempt.UnsettledCount,
                    Passed = ReportingMath.Passed(
                        attempt.AutoPointsAwarded,
                        attempt.AutoPointsAvailable,
                        attempt.FinalPointsAwarded,
                        attempt.TotalPointsAvailable),
                    SnapshotId = assignment.SnapshotId,
                    QuizRowVersion = assignment.QuizRowVersion
                });
            }
        }

        return rows
            .OrderByDescending(row => row.AutoPointsAwarded)
            .ThenBy(row => row.AttemptId)
            .ToList();
    }

    private List<TimeReportRow> BuildTimeRows(ReportScope scope)
    {
        var rows = new List<TimeReportRow>();
        foreach (var assignment in scope.Assignments)
        {
            foreach (var attempt in SubmittedAttempts(scope, assignment.AssignmentId))
            {
                var taken = ReportingMath.Elapsed(
                    assignment.Mode == "live",
                    attempt.StartedAtUtc,
                    attempt.SubmittedAtUtc!.Value,
                    assignment.TotalPausedSeconds);
                rows.Add(new TimeReportRow
                {
                    AttemptId = attempt.AttemptId,
                    AssignmentId = assignment.AssignmentId,
                    OpeningId = assignment.OpeningId,
                    OpeningTitle = Title(scope, assignment.OpeningId),
                    QuizId = assignment.QuizId,
                    QuizTitle = assignment.SnapshotTitle,
                    CandidateEmail = assignment.CandidateEmail,
                    Mode = assignment.Mode,
                    DurationSeconds = taken.DurationSeconds,
                    PauseTracked = taken.PauseTracked,
                    DurationNote = taken.Note,
                    SnapshotId = assignment.SnapshotId,
                    QuizRowVersion = assignment.QuizRowVersion
                });
            }
        }

        return rows
            .OrderBy(row => row.DurationSeconds)
            .ThenBy(row => row.AttemptId)
            .ToList();
    }

    private List<QuestionReportRow> BuildQuestionRows(ReportScope scope)
    {
        var groups = new Dictionary<(Guid QuizId, Guid QuestionId), QuestionAccumulator>();
        foreach (var assignment in scope.Assignments)
        {
            foreach (var attempt in SubmittedAttempts(scope, assignment.AssignmentId))
            {
                var autoScore = ReportingMath.AutoScore(attempt.AutoPointsAwarded, attempt.AutoPointsAvailable);
                foreach (var item in attempt.Items)
                {
                    if (!string.Equals(item.Status, "scored", StringComparison.Ordinal)
                        || !string.Equals(item.ScoringMode, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var key = (assignment.QuizId, item.QuestionId);
                    if (!groups.TryGetValue(key, out var accumulator))
                    {
                        var question = assignment.Questions.FirstOrDefault(candidate => candidate.QuestionId == item.QuestionId);
                        accumulator = new QuestionAccumulator(
                            assignment.OpeningId,
                            assignment.QuizId,
                            assignment.SnapshotTitle,
                            question?.SortOrder ?? item.SortOrder,
                            question?.Stem ?? "");
                        groups[key] = accumulator;
                    }

                    accumulator.Samples.Add((autoScore, item.PointsAwarded == item.Points));
                    accumulator.Snapshots.Add(assignment.SnapshotId);
                }
            }
        }

        return groups
            .Select(pair =>
            {
                var stats = ReportingMath.QuestionStats(pair.Value.Samples);
                return new QuestionReportRow
                {
                    QuestionId = pair.Key.QuestionId,
                    SortOrder = pair.Value.SortOrder,
                    Stem = pair.Value.Stem,
                    OpeningId = pair.Value.OpeningId,
                    OpeningTitle = Title(scope, pair.Value.OpeningId),
                    QuizId = pair.Value.QuizId,
                    QuizTitle = pair.Value.QuizTitle,
                    SettledCount = pair.Value.Samples.Count,
                    Difficulty = stats.Difficulty,
                    Discrimination = stats.Discrimination,
                    DiscriminationHidden = stats.Discrimination is null,
                    SnapshotCount = pair.Value.Snapshots.Count
                };
            })
            .OrderBy(row => row.Difficulty)
            .ThenBy(row => row.QuestionId)
            .ToList();
    }

    private List<CandidateReportRow> BuildCandidateRows(ReportScope scope)
    {
        var rows = new List<CandidateReportRow>();
        var restrict = scope.Filter.HasSubmittedConstraint || scope.Filter.HasScoreConstraint;
        foreach (var assignment in scope.Assignments)
        {
            var matched = RelevantAttempts(scope, assignment.AssignmentId);
            if (restrict)
            {
                foreach (var attempt in matched)
                {
                    rows.Add(CandidateFrom(scope, assignment, attempt));
                }

                continue;
            }

            if (!scope.Attempts.TryGetValue(assignment.AssignmentId, out var attempts) || attempts.Count == 0)
            {
                rows.Add(CandidateFrom(scope, assignment, null));
                continue;
            }

            foreach (var attempt in attempts)
            {
                rows.Add(CandidateFrom(scope, assignment, attempt));
            }
        }

        var ordered = rows
            .OrderByDescending(row => row.AutoPointsAwarded.HasValue)
            .ThenByDescending(row => row.AutoPointsAwarded)
            .ThenBy(row => row.DurationSeconds.HasValue ? 0 : 1)
            .ThenBy(row => row.DurationSeconds)
            .ThenBy(row => row.CandidateEmail, StringComparer.Ordinal)
            .ThenBy(row => row.AssignmentId)
            .ThenBy(row => row.AttemptId)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Rank = index + 1;
        }

        return ordered;
    }

    private CandidateReportRow CandidateFrom(ReportScope scope, ReportAssignmentRow assignment, ReportAttemptRow? attempt)
    {
        int? duration = null;
        if (attempt?.SubmittedAtUtc is { } submitted)
        {
            duration = ReportingMath.Elapsed(
                assignment.Mode == "live",
                attempt.StartedAtUtc,
                submitted,
                assignment.TotalPausedSeconds).DurationSeconds;
        }

        var submittedAttempt = attempt?.Status == "submitted";
        return new CandidateReportRow
        {
            CandidateEmail = assignment.CandidateEmail,
            Status = assignment.Status,
            AutoPointsAwarded = submittedAttempt ? attempt!.AutoPointsAwarded : null,
            AutoPointsAvailable = submittedAttempt ? attempt!.AutoPointsAvailable : null,
            DurationSeconds = duration,
            Mode = assignment.Mode,
            SnapshotId = assignment.SnapshotId,
            QuizRowVersion = assignment.QuizRowVersion,
            AssignmentId = assignment.AssignmentId,
            AttemptId = attempt?.AttemptId,
            OpeningId = assignment.OpeningId,
            OpeningTitle = Title(scope, assignment.OpeningId),
            QuizId = assignment.QuizId,
            QuizTitle = assignment.SnapshotTitle
        };
    }

    private List<PendingReviewReportRow> BuildPendingRows(ReportScope scope)
    {
        var now = _clock.UtcNow;
        var rows = new List<PendingReviewReportRow>();
        foreach (var assignment in scope.Assignments.Where(row => row.Status == "pendingReview"))
        {
            var attempt = SubmittedAttempts(scope, assignment.AssignmentId)
                .OrderByDescending(row => row.SubmittedAtUtc)
                .FirstOrDefault();
            if (attempt?.SubmittedAtUtc is not { } submitted)
            {
                continue;
            }

            var age = Math.Max(0, (now - submitted).TotalHours);
            if (scope.Filter.HasPendingAgeConstraint && !scope.Filter.PendingAgeHours.Matches(age))
            {
                continue;
            }

            rows.Add(new PendingReviewReportRow
            {
                AssignmentId = assignment.AssignmentId,
                AttemptId = attempt.AttemptId,
                OpeningId = assignment.OpeningId,
                OpeningTitle = Title(scope, assignment.OpeningId),
                QuizId = assignment.QuizId,
                QuizTitle = assignment.SnapshotTitle,
                CandidateEmail = assignment.CandidateEmail,
                Mode = assignment.Mode,
                SubmittedAtUtc = submitted,
                AgeHours = Math.Round(age, 4, MidpointRounding.AwayFromZero),
                UnsettledCount = attempt.UnsettledCount,
                SnapshotId = assignment.SnapshotId,
                QuizRowVersion = assignment.QuizRowVersion
            });
        }

        return rows
            .OrderByDescending(row => row.AgeHours)
            .ThenBy(row => row.AssignmentId)
            .ToList();
    }

    private static IReadOnlyList<ReportAttemptRow> RelevantAttempts(ReportScope scope, Guid assignmentId)
    {
        if (!scope.Attempts.TryGetValue(assignmentId, out var attempts))
        {
            return [];
        }

        if (!scope.Filter.HasSubmittedConstraint && !scope.Filter.HasScoreConstraint)
        {
            return attempts;
        }

        return attempts.Where(attempt => AttemptMatches(scope, attempt)).ToArray();
    }

    private static IEnumerable<ReportAttemptRow> SubmittedAttempts(ReportScope scope, Guid assignmentId)
        => RelevantAttempts(scope, assignmentId)
            .Where(attempt => attempt.Status == "submitted" && attempt.SubmittedAtUtc is not null);

    private static bool AttemptMatches(ReportScope scope, ReportAttemptRow attempt)
    {
        if (scope.Filter.HasSubmittedConstraint)
        {
            if (attempt.SubmittedAtUtc is not { } submitted || !scope.Filter.Submitted.Matches(submitted))
            {
                return false;
            }
        }

        if (scope.Filter.HasScoreConstraint && !scope.Filter.Score.Matches(attempt.AutoPointsAwarded))
        {
            return false;
        }

        return true;
    }

    private static string Title(ReportScope scope, Guid openingId)
        => scope.OpeningTitles.TryGetValue(openingId, out var title) ? title : openingId.ToString();

    private static List<T> Page<T>(IReadOnlyList<T> rows, PageRequest request)
        => rows.Skip(request.Skip).Take(request.PageSize).ToList();

    private static readonly string[] FunnelHeaders =
    [
        "openingId", "openingTitle", "quizId", "quizTitle", "assigned", "appeared", "submitted",
        "completed", "pendingReview", "appearedRate", "submittedRate", "completedRate"
    ];

    private static readonly string[] ScoreHeaders =
    [
        "openingId", "openingTitle", "quizId", "quizTitle", "assignmentId", "attemptId", "snapshotId",
        "quizRowVersion", "candidateEmail", "mode", "status", "resultStatus", "autoPointsAwarded",
        "autoPointsAvailable", "totalPointsAvailable", "finalPointsAwarded", "scoreBasis",
        "unsettledCount", "passed"
    ];

    private static readonly string[] TimeHeaders =
    [
        "openingId", "openingTitle", "quizId", "quizTitle", "assignmentId", "attemptId", "snapshotId",
        "quizRowVersion", "candidateEmail", "mode", "durationSeconds", "pauseTracked"
    ];

    private static readonly string[] QuestionHeaders =
    [
        "openingId", "openingTitle", "quizId", "quizTitle", "questionId", "sortOrder", "settledCount",
        "difficulty", "discrimination"
    ];

    private static readonly string[] CandidateHeaders =
    [
        "rank", "openingId", "openingTitle", "quizId", "quizTitle", "candidateEmail", "status",
        "autoPointsAwarded", "autoPointsAvailable", "durationSeconds", "mode", "snapshotId",
        "quizRowVersion", "assignmentId", "attemptId"
    ];

    private static readonly string[] PendingHeaders =
    [
        "openingId", "openingTitle", "quizId", "quizTitle", "assignmentId", "attemptId", "candidateEmail",
        "mode", "submittedAtUtc", "ageHours", "unsettledCount", "snapshotId", "quizRowVersion"
    ];

    private static List<IReadOnlyList<string?>> FunnelCsv(IReadOnlyList<FunnelReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.OpeningId?.ToString(),
            row.OpeningTitle,
            row.QuizId?.ToString(),
            row.QuizTitle,
            row.Assigned.ToString(CultureInfo.InvariantCulture),
            row.Appeared.ToString(CultureInfo.InvariantCulture),
            row.Submitted.ToString(CultureInfo.InvariantCulture),
            row.Completed.ToString(CultureInfo.InvariantCulture),
            row.PendingReview.ToString(CultureInfo.InvariantCulture),
            Dec(row.AppearedRate),
            Dec(row.SubmittedRate),
            Dec(row.CompletedRate)
        }).ToList();

    private static List<IReadOnlyList<string?>> ScoreCsv(IReadOnlyList<ScoreReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.OpeningId.ToString(),
            row.OpeningTitle,
            row.QuizId.ToString(),
            row.QuizTitle,
            row.AssignmentId.ToString(),
            row.AttemptId.ToString(),
            row.SnapshotId.ToString(),
            row.QuizRowVersion.ToString(CultureInfo.InvariantCulture),
            row.CandidateEmail,
            row.Mode,
            row.Status,
            row.ResultStatus,
            Dec(row.AutoPointsAwarded),
            Dec(row.AutoPointsAvailable),
            Dec(row.TotalPointsAvailable),
            row.FinalPointsAwarded is { } final ? Dec(final) : "",
            row.ScoreBasis,
            row.UnsettledCount.ToString(CultureInfo.InvariantCulture),
            row.Passed switch { true => "true", false => "false", _ => "" }
        }).ToList();

    private static List<IReadOnlyList<string?>> TimeCsv(IReadOnlyList<TimeReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.OpeningId.ToString(),
            row.OpeningTitle,
            row.QuizId.ToString(),
            row.QuizTitle,
            row.AssignmentId.ToString(),
            row.AttemptId.ToString(),
            row.SnapshotId.ToString(),
            row.QuizRowVersion.ToString(CultureInfo.InvariantCulture),
            row.CandidateEmail,
            row.Mode,
            row.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            row.PauseTracked ? "true" : "false"
        }).ToList();

    private static List<IReadOnlyList<string?>> QuestionCsv(IReadOnlyList<QuestionReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.OpeningId.ToString(),
            row.OpeningTitle,
            row.QuizId.ToString(),
            row.QuizTitle,
            row.QuestionId.ToString(),
            row.SortOrder.ToString(CultureInfo.InvariantCulture),
            row.SettledCount.ToString(CultureInfo.InvariantCulture),
            Dec(row.Difficulty),
            row.Discrimination is { } value ? Dec(value) : ""
        }).ToList();

    private static List<IReadOnlyList<string?>> CandidateCsv(IReadOnlyList<CandidateReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.Rank.ToString(CultureInfo.InvariantCulture),
            row.OpeningId.ToString(),
            row.OpeningTitle,
            row.QuizId.ToString(),
            row.QuizTitle,
            row.CandidateEmail,
            row.Status,
            row.AutoPointsAwarded is { } awarded ? Dec(awarded) : "",
            row.AutoPointsAvailable is { } available ? Dec(available) : "",
            row.DurationSeconds?.ToString(CultureInfo.InvariantCulture),
            row.Mode,
            row.SnapshotId.ToString(),
            row.QuizRowVersion.ToString(CultureInfo.InvariantCulture),
            row.AssignmentId.ToString(),
            row.AttemptId?.ToString()
        }).ToList();

    private static List<IReadOnlyList<string?>> PendingCsv(IReadOnlyList<PendingReviewReportRow> rows)
        => rows.Select(row => (IReadOnlyList<string?>)new string?[]
        {
            row.OpeningId.ToString(),
            row.OpeningTitle,
            row.QuizId.ToString(),
            row.QuizTitle,
            row.AssignmentId.ToString(),
            row.AttemptId?.ToString(),
            row.CandidateEmail,
            row.Mode,
            row.SubmittedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
            row.AgeHours.ToString(CultureInfo.InvariantCulture),
            row.UnsettledCount.ToString(CultureInfo.InvariantCulture),
            row.SnapshotId.ToString(),
            row.QuizRowVersion.ToString(CultureInfo.InvariantCulture)
        }).ToList();

    private static string Dec(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class QuestionAccumulator(Guid openingId, Guid quizId, string quizTitle, int sortOrder, string stem)
    {
        public Guid OpeningId { get; } = openingId;
        public Guid QuizId { get; } = quizId;
        public string QuizTitle { get; } = quizTitle;
        public int SortOrder { get; } = sortOrder;
        public string Stem { get; } = stem;
        public List<(decimal AutoScore, bool FullPoints)> Samples { get; } = [];
        public HashSet<Guid> Snapshots { get; } = [];
    }

    private sealed record ReportScope(
        ReportFilterSet Filter,
        IReadOnlyList<ReportAssignmentRow> Assignments,
        IReadOnlyDictionary<Guid, IReadOnlyList<ReportAttemptRow>> Attempts,
        IReadOnlyDictionary<Guid, string> OpeningTitles,
        bool DefaultWindowApplied,
        DateTimeOffset? CreatedFrom,
        DateTimeOffset? CreatedTo)
    {
        public static ReportScope Empty(bool defaultWindow, DateConstraint created)
            => new(
                new ReportFilterSet { Empty = true },
                [],
                new Dictionary<Guid, IReadOnlyList<ReportAttemptRow>>(),
                new Dictionary<Guid, string>(),
                defaultWindow,
                created.Lower,
                created.Upper);
    }
}
