using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Search.Domain;

namespace InterviewQuiz.Search.Application;

public static class ReportKeys
{
    public const string Funnel = "funnel";
    public const string Scores = "scores";
    public const string Time = "time";
    public const string Questions = "questions";
    public const string Candidates = "candidates";
    public const string PendingReview = "pendingReview";

    public static bool TryParseRoute(string? route, out string key)
    {
        key = route?.Trim() switch
        {
            Funnel => Funnel,
            Scores => Scores,
            Time => Time,
            Questions => Questions,
            Candidates => Candidates,
            PendingReview => PendingReview,
            "pending-review" => PendingReview,
            _ => ""
        };
        return key.Length > 0;
    }

    public static string Route(string key)
        => key == PendingReview ? "pending-review" : key;

    public static string KeyFor(FilterTarget target)
        => target switch
        {
            FilterTarget.ReportsFunnel => Funnel,
            FilterTarget.ReportsScores => Scores,
            FilterTarget.ReportsTime => Time,
            FilterTarget.ReportsQuestions => Questions,
            FilterTarget.ReportsCandidates => Candidates,
            FilterTarget.ReportsPendingReview => PendingReview,
            _ => throw new DomainException("Unknown filter target.")
        };

    public static bool IsReport(FilterTarget target)
        => target is FilterTarget.ReportsFunnel
            or FilterTarget.ReportsScores
            or FilterTarget.ReportsTime
            or FilterTarget.ReportsQuestions
            or FilterTarget.ReportsCandidates
            or FilterTarget.ReportsPendingReview;
}

public sealed class ReportFilterCriteria
{
    public List<ReportFilterChip> Filters { get; set; } = [];
}

public sealed class ReportFilterChip
{
    public string Field { get; set; } = "";

    public string Operator { get; set; } = "";

    public JsonElement Value { get; set; }
}

public readonly record struct DateConstraint(
    DateTimeOffset? Lower,
    bool LowerExclusive,
    DateTimeOffset? Upper,
    bool UpperExclusive,
    bool Empty)
{
    public static DateConstraint Unconstrained { get; } = new(null, false, null, false, false);

    public bool Matches(DateTimeOffset value)
    {
        if (Empty)
        {
            return false;
        }

        if (Lower is { } lower)
        {
            if (LowerExclusive)
            {
                if (value <= lower)
                {
                    return false;
                }
            }
            else if (value < lower)
            {
                return false;
            }
        }

        if (Upper is { } upper)
        {
            if (UpperExclusive)
            {
                if (value >= upper)
                {
                    return false;
                }
            }
            else if (value > upper)
            {
                return false;
            }
        }

        return true;
    }
}

public readonly record struct DecimalConstraint(
    decimal? Lower,
    bool LowerExclusive,
    decimal? Upper,
    bool UpperExclusive,
    bool Empty)
{
    public static DecimalConstraint Unconstrained { get; } = new(null, false, null, false, false);

    public bool Matches(decimal value)
    {
        if (Empty)
        {
            return false;
        }

        if (Lower is { } lower)
        {
            if (LowerExclusive)
            {
                if (value <= lower)
                {
                    return false;
                }
            }
            else if (value < lower)
            {
                return false;
            }
        }

        if (Upper is { } upper)
        {
            if (UpperExclusive)
            {
                if (value >= upper)
                {
                    return false;
                }
            }
            else if (value > upper)
            {
                return false;
            }
        }

        return true;
    }
}

public readonly record struct DoubleConstraint(
    double? Lower,
    bool LowerExclusive,
    double? Upper,
    bool UpperExclusive,
    bool Empty)
{
    public static DoubleConstraint Unconstrained { get; } = new(null, false, null, false, false);

    public bool Matches(double value)
    {
        if (Empty)
        {
            return false;
        }

        if (Lower is { } lower)
        {
            if (LowerExclusive)
            {
                if (value <= lower)
                {
                    return false;
                }
            }
            else if (value < lower)
            {
                return false;
            }
        }

        if (Upper is { } upper)
        {
            if (UpperExclusive)
            {
                if (value >= upper)
                {
                    return false;
                }
            }
            else if (value > upper)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class ReportFilterSet
{
    public bool Empty { get; init; }
    public IReadOnlyCollection<Guid>? OpeningIds { get; init; }
    public IReadOnlyCollection<Guid>? QuizIds { get; init; }
    public DateConstraint Created { get; init; } = DateConstraint.Unconstrained;
    public bool HasCreatedConstraint { get; init; }
    public DateConstraint Submitted { get; init; } = DateConstraint.Unconstrained;
    public bool HasSubmittedConstraint { get; init; }
    public IReadOnlyCollection<string>? Statuses { get; init; }
    public string? Mode { get; init; }
    public Guid? SnapshotId { get; init; }
    public bool HasSnapshotConstraint { get; init; }
    public IReadOnlyCollection<long>? QuizRowVersions { get; init; }
    public DecimalConstraint Score { get; init; } = DecimalConstraint.Unconstrained;
    public bool HasScoreConstraint { get; init; }
    public DoubleConstraint PendingAgeHours { get; init; } = DoubleConstraint.Unconstrained;
    public bool HasPendingAgeConstraint { get; init; }
}

public static class ReportFilterParser
{
    public const string UnknownFieldMessagePrefix = "Unknown filter field";
    public const string NotAllowedMessage = "is not allowed on this report";

    private static readonly JsonSerializerOptions StrictOptions = Create(unmappedDisallowed: true);
    private static readonly JsonSerializerOptions PersistOptions = Create(unmappedDisallowed: false);

    private static readonly Dictionary<string, string[]> CommonFields = new(StringComparer.Ordinal)
    {
        ["openingId"] = ["is", "in"],
        ["quizId"] = ["is", "in"],
        ["createdAtUtc"] = ["between", ">", "<"],
        ["assignedAt"] = ["between", ">", "<"],
        ["submittedAtUtc"] = ["between", ">", "<"],
        ["status"] = ["is", "in"],
        ["mode"] = ["is"],
        ["snapshotId"] = ["is"],
        ["quizRowVersion"] = ["is", "in"]
    };

    private static readonly Dictionary<string, string[]> ScoreFields = new(StringComparer.Ordinal)
    {
        ["autoPointsAwarded"] = [">", "<", "between"],
        ["score"] = [">", "<", "between"]
    };

    private static readonly Dictionary<string, string[]> PendingFields = new(StringComparer.Ordinal)
    {
        ["pendingAgeHours"] = [">", "between"]
    };

    private static readonly HashSet<string> StatusValues = new(StringComparer.Ordinal)
    {
        "notStarted", "inProgress", "pendingReview", "completed"
    };

    private static readonly HashSet<string> ModeValues = new(StringComparer.Ordinal)
    {
        "async", "live"
    };

    public static JsonDocument Canonicalize(FilterTarget target, JsonElement criteria)
    {
        var key = ReportKeys.KeyFor(target);
        var normalized = Normalize(key, criteria);
        return JsonSerializer.SerializeToDocument(normalized, PersistOptions);
    }

    public static ReportFilterSet Parse(string reportKey, JsonElement criteria)
    {
        var chips = Read(reportKey, criteria);
        return Compile(chips);
    }

    public static ReportFilterCriteria Normalize(string reportKey, JsonElement criteria)
    {
        var chips = Read(reportKey, criteria);
        return new ReportFilterCriteria
        {
            Filters = chips.Select(chip => new ReportFilterChip
            {
                Field = CanonicalField(chip.Field),
                Operator = chip.Operator,
                Value = chip.Value.Clone()
            }).ToList()
        };
    }

    private static List<ReportFilterChip> Read(string reportKey, JsonElement criteria)
    {
        if (!ReportKeys.TryParseRoute(reportKey, out var key))
        {
            throw new DomainException("Unknown report.");
        }

        if (criteria.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (criteria.ValueKind != JsonValueKind.Object)
        {
            throw new DomainException("Criteria must be a JSON object.");
        }

        ReportFilterCriteria parsed;
        try
        {
            parsed = criteria.Deserialize<ReportFilterCriteria>(StrictOptions)
                ?? throw new DomainException("Criteria must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new DomainException("Filter criteria does not match the report shape.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new DomainException("Filter criteria does not match the report shape.", ex);
        }

        var allowed = FieldsFor(key);
        var chips = parsed.Filters ?? [];
        foreach (var chip in chips)
        {
            if (string.IsNullOrWhiteSpace(chip.Field))
            {
                throw new DomainException("Filter field is required.");
            }

            if (string.IsNullOrWhiteSpace(chip.Operator))
            {
                throw new DomainException("Filter operator is required.");
            }

            var field = chip.Field.Trim();
            var op = chip.Operator.Trim();
            if (!KnownFields().ContainsKey(field))
            {
                throw new DomainException($"{UnknownFieldMessagePrefix} '{field}'.");
            }

            if (!allowed.ContainsKey(field))
            {
                throw new DomainException($"Filter field '{field}' {NotAllowedMessage}.");
            }

            if (!allowed[field].Contains(op, StringComparer.Ordinal))
            {
                throw new DomainException($"Operator '{op}' is not allowed for '{field}'.");
            }

            ValidateValue(field, op, chip.Value);
            chip.Field = field;
            chip.Operator = op;
        }

        return chips;
    }

    private static ReportFilterSet Compile(IReadOnlyList<ReportFilterChip> chips)
    {
        IReadOnlyCollection<Guid>? openingIds = null;
        IReadOnlyCollection<Guid>? quizIds = null;
        var created = DateConstraint.Unconstrained;
        var hasCreated = false;
        var submitted = DateConstraint.Unconstrained;
        var hasSubmitted = false;
        IReadOnlyCollection<string>? statuses = null;
        string? mode = null;
        Guid? snapshotId = null;
        var hasSnapshot = false;
        IReadOnlyCollection<long>? versions = null;
        var score = DecimalConstraint.Unconstrained;
        var hasScore = false;
        var age = DoubleConstraint.Unconstrained;
        var hasAge = false;
        var empty = false;

        foreach (var chip in chips)
        {
            var field = CanonicalField(chip.Field);
            switch (field)
            {
                case "openingId":
                    openingIds = AndSet(openingIds, GuidValues(chip), ref empty);
                    break;
                case "quizId":
                    quizIds = AndSet(quizIds, GuidValues(chip), ref empty);
                    break;
                case "createdAtUtc":
                    hasCreated = true;
                    created = AndDate(created, chip);
                    empty |= created.Empty;
                    break;
                case "submittedAtUtc":
                    hasSubmitted = true;
                    submitted = AndDate(submitted, chip);
                    empty |= submitted.Empty;
                    break;
                case "status":
                    statuses = AndSet(statuses, StringValues(chip), ref empty, StringComparer.Ordinal);
                    break;
                case "mode":
                    var nextMode = StringValues(chip).Single();
                    if (mode is not null && !string.Equals(mode, nextMode, StringComparison.Ordinal))
                    {
                        empty = true;
                    }

                    mode = nextMode;
                    break;
                case "snapshotId":
                    hasSnapshot = true;
                    var nextSnapshot = GuidValues(chip).Single();
                    if (snapshotId is not null && snapshotId != nextSnapshot)
                    {
                        empty = true;
                    }

                    snapshotId = nextSnapshot;
                    break;
                case "quizRowVersion":
                    versions = AndSet(versions, LongValues(chip), ref empty);
                    break;
                case "autoPointsAwarded":
                    hasScore = true;
                    score = AndDecimal(score, chip);
                    empty |= score.Empty;
                    break;
                case "pendingAgeHours":
                    hasAge = true;
                    age = AndDouble(age, chip);
                    empty |= age.Empty;
                    break;
            }

            if (empty)
            {
                break;
            }
        }

        return new ReportFilterSet
        {
            Empty = empty,
            OpeningIds = empty ? [] : openingIds,
            QuizIds = empty ? [] : quizIds,
            Created = empty ? new DateConstraint(null, false, null, false, true) : created,
            HasCreatedConstraint = hasCreated,
            Submitted = empty ? new DateConstraint(null, false, null, false, true) : submitted,
            HasSubmittedConstraint = hasSubmitted,
            Statuses = empty ? [] : statuses,
            Mode = empty ? null : mode,
            HasSnapshotConstraint = hasSnapshot,
            SnapshotId = empty ? null : snapshotId,
            QuizRowVersions = empty ? [] : versions,
            Score = empty ? new DecimalConstraint(null, false, null, false, true) : score,
            HasScoreConstraint = hasScore,
            PendingAgeHours = empty ? new DoubleConstraint(null, false, null, false, true) : age,
            HasPendingAgeConstraint = hasAge
        };
    }

    private static Dictionary<string, string[]> FieldsFor(string key)
    {
        var fields = new Dictionary<string, string[]>(CommonFields, StringComparer.Ordinal);
        if (key is ReportKeys.Scores or ReportKeys.Candidates)
        {
            foreach (var pair in ScoreFields)
            {
                fields[pair.Key] = pair.Value;
            }
        }

        if (key == ReportKeys.PendingReview)
        {
            foreach (var pair in PendingFields)
            {
                fields[pair.Key] = pair.Value;
            }
        }

        return fields;
    }

    private static Dictionary<string, string[]> KnownFields()
    {
        var fields = new Dictionary<string, string[]>(CommonFields, StringComparer.Ordinal);
        foreach (var pair in ScoreFields)
        {
            fields[pair.Key] = pair.Value;
        }

        foreach (var pair in PendingFields)
        {
            fields[pair.Key] = pair.Value;
        }

        return fields;
    }

    private static string CanonicalField(string field)
        => field switch
        {
            "assignedAt" => "createdAtUtc",
            "score" => "autoPointsAwarded",
            _ => field
        };

    private static void ValidateValue(string field, string op, JsonElement value)
    {
        var canonical = CanonicalField(field);
        switch (canonical)
        {
            case "openingId":
            case "quizId":
            case "snapshotId":
                RequireGuids(field, op, value);
                break;
            case "createdAtUtc":
            case "submittedAtUtc":
                RequireInstants(field, op, value);
                break;
            case "status":
                RequireStrings(field, op, value, StatusValues, "status");
                break;
            case "mode":
                RequireStrings(field, op, value, ModeValues, "mode");
                break;
            case "quizRowVersion":
                RequireLongs(field, op, value);
                break;
            case "autoPointsAwarded":
                RequireDecimals(field, op, value);
                break;
            case "pendingAgeHours":
                RequireDoubles(field, op, value);
                break;
        }
    }

    private static void RequireGuids(string field, string op, JsonElement value)
    {
        foreach (var item in Items(field, op, value))
        {
            if (item.ValueKind != JsonValueKind.String || !Guid.TryParse(item.GetString(), out var id) || id == Guid.Empty)
            {
                throw new DomainException($"Filter value for {field} is invalid.");
            }
        }
    }

    private static void RequireInstants(string field, string op, JsonElement value)
    {
        if (op == "between")
        {
            var (from, to) = Between(field, value);
            var start = ParseInstant(field, from);
            var end = ParseInstant(field, to);
            if (start > end)
            {
                throw new DomainException("Filter between values are out of order.");
            }

            return;
        }

        _ = ParseInstant(field, value);
    }

    private static void RequireStrings(
        string field,
        string op,
        JsonElement value,
        HashSet<string> allowed,
        string label)
    {
        foreach (var item in Items(field, op, value))
        {
            if (item.ValueKind != JsonValueKind.String
                || item.GetString() is not { } text
                || !allowed.Contains(text))
            {
                throw new DomainException($"Filter value for {label} is invalid.");
            }
        }
    }

    private static void RequireLongs(string field, string op, JsonElement value)
    {
        if (op == "between")
        {
            throw new DomainException($"Operator 'between' is not allowed for '{field}'.");
        }

        foreach (var item in Items(field, op, value))
        {
            if (!TryLong(item, out var number) || number < 0)
            {
                throw new DomainException($"Filter value for {field} is invalid.");
            }
        }
    }

    private static void RequireDecimals(string field, string op, JsonElement value)
    {
        if (op == "between")
        {
            var (from, to) = Between(field, value);
            var start = ParseDecimal(field, from);
            var end = ParseDecimal(field, to);
            if (start > end)
            {
                throw new DomainException("Filter between values are out of order.");
            }

            return;
        }

        _ = ParseDecimal(field, value);
    }

    private static void RequireDoubles(string field, string op, JsonElement value)
    {
        if (op == "between")
        {
            var (from, to) = Between(field, value);
            var start = ParseDouble(field, from);
            var end = ParseDouble(field, to);
            if (start > end)
            {
                throw new DomainException("Filter between values are out of order.");
            }

            return;
        }

        _ = ParseDouble(field, value);
    }

    private static IEnumerable<JsonElement> Items(string field, string op, JsonElement value)
    {
        if (op == "in")
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
            {
                throw new DomainException($"Filter value for {field} is invalid.");
            }

            return value.EnumerateArray().ToArray();
        }

        if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new DomainException($"Filter value for {field} is invalid.");
        }

        return [value];
    }

    private static (JsonElement From, JsonElement To) Between(string field, JsonElement value)
    {
        BetweenValue? parsed;
        try
        {
            parsed = value.Deserialize<BetweenValue>(StrictOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Filter value for {field} is invalid.", ex);
        }

        if (parsed is null
            || parsed.From.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            || parsed.To.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new DomainException($"Filter value for {field} is invalid.");
        }

        return (parsed.From, parsed.To);
    }

    private static IReadOnlyCollection<Guid> GuidValues(ReportFilterChip chip)
        => Items(chip.Field, chip.Operator, chip.Value)
            .Select(item => Guid.Parse(item.GetString()!))
            .ToArray();

    private static IReadOnlyCollection<string> StringValues(ReportFilterChip chip)
        => Items(chip.Field, chip.Operator, chip.Value)
            .Select(item => item.GetString()!)
            .ToArray();

    private static IReadOnlyCollection<long> LongValues(ReportFilterChip chip)
        => Items(chip.Field, chip.Operator, chip.Value)
            .Select(item =>
            {
                TryLong(item, out var number);
                return number;
            })
            .ToArray();

    private static DateConstraint AndDate(DateConstraint current, ReportFilterChip chip)
    {
        DateTimeOffset? lower = current.Lower;
        var lowerExclusive = current.LowerExclusive;
        DateTimeOffset? upper = current.Upper;
        var upperExclusive = current.UpperExclusive;

        if (chip.Operator == "between")
        {
            var (from, to) = Between(chip.Field, chip.Value);
            TightenLower(ref lower, ref lowerExclusive, ParseInstant(chip.Field, from), nextExclusive: false);
            TightenUpper(ref upper, ref upperExclusive, ParseInstant(chip.Field, to), nextExclusive: false);
        }
        else if (chip.Operator == ">")
        {
            TightenLower(ref lower, ref lowerExclusive, ParseInstant(chip.Field, chip.Value), nextExclusive: true);
        }
        else
        {
            TightenUpper(ref upper, ref upperExclusive, ParseInstant(chip.Field, chip.Value), nextExclusive: true);
        }

        var empty = BoundsEmpty(lower, lowerExclusive, upper, upperExclusive);
        return new DateConstraint(lower, lowerExclusive, upper, upperExclusive, empty);
    }

    private static DecimalConstraint AndDecimal(DecimalConstraint current, ReportFilterChip chip)
    {
        decimal? lower = current.Lower;
        var lowerExclusive = current.LowerExclusive;
        decimal? upper = current.Upper;
        var upperExclusive = current.UpperExclusive;

        if (chip.Operator == "between")
        {
            var (from, to) = Between(chip.Field, chip.Value);
            TightenLower(ref lower, ref lowerExclusive, ParseDecimal(chip.Field, from), nextExclusive: false);
            TightenUpper(ref upper, ref upperExclusive, ParseDecimal(chip.Field, to), nextExclusive: false);
        }
        else if (chip.Operator == ">")
        {
            TightenLower(ref lower, ref lowerExclusive, ParseDecimal(chip.Field, chip.Value), nextExclusive: true);
        }
        else
        {
            TightenUpper(ref upper, ref upperExclusive, ParseDecimal(chip.Field, chip.Value), nextExclusive: true);
        }

        var empty = BoundsEmpty(lower, lowerExclusive, upper, upperExclusive);
        return new DecimalConstraint(lower, lowerExclusive, upper, upperExclusive, empty);
    }

    private static DoubleConstraint AndDouble(DoubleConstraint current, ReportFilterChip chip)
    {
        double? lower = current.Lower;
        var lowerExclusive = current.LowerExclusive;
        double? upper = current.Upper;
        var upperExclusive = current.UpperExclusive;

        if (chip.Operator == "between")
        {
            var (from, to) = Between(chip.Field, chip.Value);
            TightenLower(ref lower, ref lowerExclusive, ParseDouble(chip.Field, from), nextExclusive: false);
            TightenUpper(ref upper, ref upperExclusive, ParseDouble(chip.Field, to), nextExclusive: false);
        }
        else
        {
            TightenLower(ref lower, ref lowerExclusive, ParseDouble(chip.Field, chip.Value), nextExclusive: true);
        }

        var empty = BoundsEmpty(lower, lowerExclusive, upper, upperExclusive);
        return new DoubleConstraint(lower, lowerExclusive, upper, upperExclusive, empty);
    }

    private static void TightenLower<T>(ref T? current, ref bool exclusive, T next, bool nextExclusive)
        where T : struct, IComparable<T>
    {
        if (current is null || next.CompareTo(current.Value) > 0)
        {
            current = next;
            exclusive = nextExclusive;
            return;
        }

        if (next.CompareTo(current.Value) == 0 && nextExclusive)
        {
            exclusive = true;
        }
    }

    private static void TightenUpper<T>(ref T? current, ref bool exclusive, T next, bool nextExclusive)
        where T : struct, IComparable<T>
    {
        if (current is null || next.CompareTo(current.Value) < 0)
        {
            current = next;
            exclusive = nextExclusive;
            return;
        }

        if (next.CompareTo(current.Value) == 0 && nextExclusive)
        {
            exclusive = true;
        }
    }

    private static bool BoundsEmpty<T>(T? lower, bool lowerExclusive, T? upper, bool upperExclusive)
        where T : struct, IComparable<T>
    {
        if (lower is null || upper is null)
        {
            return false;
        }

        var compare = lower.Value.CompareTo(upper.Value);
        if (compare > 0)
        {
            return true;
        }

        return compare == 0 && (lowerExclusive || upperExclusive);
    }

    private static IReadOnlyCollection<T>? AndSet<T>(
        IReadOnlyCollection<T>? current,
        IReadOnlyCollection<T> next,
        ref bool empty,
        IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        if (current is null)
        {
            var seeded = next.Distinct(comparer).ToArray();
            if (seeded.Length == 0)
            {
                empty = true;
            }

            return seeded;
        }

        var set = current.Where(item => next.Contains(item, comparer)).Distinct(comparer).ToArray();
        if (set.Length == 0)
        {
            empty = true;
        }

        return set;
    }

    private static DateTimeOffset ParseInstant(string field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var instant))
        {
            throw new DomainException($"Filter value for {field} is invalid.");
        }

        return instant.ToUniversalTime();
    }

    private static decimal ParseDecimal(string field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        throw new DomainException($"Filter value for {field} is invalid.");
    }

    private static double ParseDouble(string field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && !double.IsNaN(number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            && !double.IsNaN(number))
        {
            return number;
        }

        throw new DomainException($"Filter value for {field} is invalid.");
    }

    private static bool TryLong(JsonElement value, out long number)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out number))
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private static JsonSerializerOptions Create(bool unmappedDisallowed)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        if (unmappedDisallowed)
        {
            options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        }

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed class BetweenValue
    {
        public JsonElement From { get; set; }
        public JsonElement To { get; set; }
    }
}
