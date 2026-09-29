using System.Diagnostics;
using InterviewQuiz.Kernel.Clock;
using InterviewQuiz.Kernel.Exceptions;
using InterviewQuiz.Kernel.Pagination;
using InterviewQuiz.Openings.Application.Contracts;
using InterviewQuiz.Openings.Domain;
using Microsoft.Extensions.Logging;

namespace InterviewQuiz.Openings.Application.Services;

public sealed class OpeningService : IOpeningService, IOpeningLookup
{
    public static readonly ActivitySource ActivitySource = new("InterviewQuiz.Openings");

    private readonly IOpeningRepository _openings;
    private readonly IClock _clock;
    private readonly ILogger<OpeningService> _logger;

    public OpeningService(
        IOpeningRepository openings,
        IClock clock,
        ILogger<OpeningService> logger)
    {
        _openings = openings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OpeningResponse> CreateAsync(CreateOpeningRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.create");
        var opening = Opening.Create(
            request.Title,
            request.JobDescription,
            request.Owner,
            request.StartDate,
            request.ExpectedCloseDate,
            request.Headcount,
            request.ExpectedExperienceYears,
            request.Handlers,
            request.Tags,
            _clock);

        await _openings.AddAsync(opening, cancellationToken);
        await _openings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created opening {OpeningId}", opening.Id);
        return Map(opening);
    }

    public async Task<OpeningResponse> UpdateAsync(UpdateOpeningRequest request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.update");
        var opening = await _openings.GetAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Opening), request.Id);

        if (opening.RowVersion != request.RowVersion)
        {
            throw new ConcurrencyException("Opening was modified by another request. Reload and retry.");
        }

        opening.Update(
            request.Title,
            request.JobDescription,
            request.Owner,
            request.StartDate,
            request.ExpectedCloseDate,
            request.Headcount,
            request.ExpectedExperienceYears,
            request.Handlers,
            request.Tags,
            _clock);

        _openings.SetExpectedRowVersion(opening, request.RowVersion);
        await _openings.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated opening {OpeningId}", opening.Id);
        return Map(opening);
    }

    public async Task<OpeningResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.get");
        var opening = await _openings.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Opening), id);

        return Map(opening);
    }

    public async Task<OpeningLookupDto?> GetOpeningAsync(Guid id, CancellationToken cancellationToken)
    {
        var opening = await _openings.GetAsync(id, cancellationToken);
        if (opening is null)
        {
            return null;
        }

        return new OpeningLookupDto(
            opening.Id,
            opening.Title,
            opening.Owner,
            opening.StartDate,
            opening.ExpectedCloseDate,
            opening.Headcount,
            opening.ExpectedExperienceYears);
    }

    public async Task<PagedResult<OpeningResponse>> ListAsync(
        OpeningListCriteria criteria,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("openings.list");
        var result = await _openings.ListAsync(criteria, page, cancellationToken);
        var items = result.Items.Select(Map).ToList();
        return new PagedResult<OpeningResponse>(items, result.Page, result.PageSize, result.TotalCount);
    }

    internal static OpeningResponse Map(Opening opening) => new()
    {
        Id = opening.Id,
        Title = opening.Title,
        JobDescription = opening.JobDescription,
        Owner = opening.Owner,
        StartDate = opening.StartDate,
        ExpectedCloseDate = opening.ExpectedCloseDate,
        Headcount = opening.Headcount,
        ExpectedExperienceYears = opening.ExpectedExperienceYears,
        Handlers = opening.Handlers,
        Tags = opening.Tags,
        RowVersion = opening.RowVersion,
        CreatedAtUtc = opening.CreatedAtUtc,
        UpdatedAtUtc = opening.UpdatedAtUtc
    };
}
