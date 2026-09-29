using FormFlow.Blazor.Services;
using FormFlow.Data.Models;

namespace FormFlow.Blazor.Tests.Respond;

/// <summary>In-memory ISurveyService for page tests.</summary>
public sealed class FakeSurveyService : ISurveyService
{
    public List<SurveyDefinition> Surveys { get; } = new();
    public Dictionary<Guid, List<QuestionDefinition>> Questions { get; } = new();
    public SubmitResult NextSubmitResult { get; set; } = new(true, new Dictionary<string, string[]>());
    public Dictionary<string, List<string>>? LastSubmitted { get; private set; }
    public SurveyResults? Results { get; set; }

    /// <summary>Makes every read fail, as when the server can't be reached.</summary>
    public bool Unreachable { get; set; }

    public Task<List<SurveyDefinition>?> GetSurveysAsync() =>
        Task.FromResult<List<SurveyDefinition>?>(Unreachable ? null : Surveys.ToList());

    /// <summary>Like the API for an administrator: every survey. Pages narrow it down for a professor.</summary>
    public Task<List<SurveyDefinition>?> GetManagedSurveysAsync() =>
        Task.FromResult<List<SurveyDefinition>?>(Unreachable ? null : Surveys.ToList());

    public Task<SurveyDefinition?> GetSurveyAsync(Guid id) =>
        Task.FromResult(Unreachable ? null : Surveys.FirstOrDefault(s => s.Id == id));

    public Task<SurveyDefinition?> GetSurveyByShareCodeAsync(string code) =>
        Task.FromResult(Surveys.FirstOrDefault(s => string.Equals(s.ShareCode, code, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Makes loading a survey's questions fail, while the survey itself still loads.</summary>
    public bool QuestionsUnreachable { get; set; }

    public Task<List<QuestionDefinition>?> GetSurveyQuestionsAsync(Guid id) =>
        Task.FromResult<List<QuestionDefinition>?>(Unreachable || QuestionsUnreachable ? null
            : Questions.TryGetValue(id, out var q) ? q : new List<QuestionDefinition>());

    public (bool Success, string? Error) NextSaveResult { get; set; } = (true, null);
    public NewSurvey? LastCreated { get; private set; }
    public (Guid Id, NewSurvey Survey)? LastUpdated { get; private set; }

    public Task<(bool Success, string? Error)> CreateSurveyAsync(NewSurvey survey)
    {
        LastCreated = survey;
        return Task.FromResult(NextSaveResult);
    }

    public Task<(bool Success, string? Error)> UpdateSurveyAsync(Guid id, NewSurvey survey)
    {
        LastUpdated = (id, survey);
        return Task.FromResult(NextSaveResult);
    }

    public (bool Success, string? Error) NextDeleteResult { get; set; } = (true, null);
    public List<Guid> Deleted { get; } = new();

    public Task<(bool Success, string? Error)> DeleteSurveyAsync(Guid id)
    {
        if (NextDeleteResult.Success)
        {
            Deleted.Add(id);
        }
        return Task.FromResult(NextDeleteResult);
    }

    /// <summary>When set, duplicating and using a template fail with this message.</summary>
    public string? NextCopyError { get; set; }
    public List<Guid> Duplicated { get; } = new();

    /// <summary>Copies the survey into a new draft, like the API does.</summary>
    public Task<(SurveyDefinition? Survey, string? Error)> DuplicateSurveyAsync(Guid id)
    {
        var source = Surveys.FirstOrDefault(s => s.Id == id);
        if (NextCopyError is not null || source is null)
        {
            return Task.FromResult<(SurveyDefinition?, string?)>((null, NextCopyError ?? "Survey not found."));
        }
        Duplicated.Add(id);
        return Task.FromResult<(SurveyDefinition?, string?)>((AddCopy(source.Id, $"Copy of {source.Title}", source.QuestionIds, source.PageBreaks), null));
    }

    public List<SurveyTemplate> Templates { get; } = new();

    /// <summary>Makes loading the templates fail.</summary>
    public bool TemplatesUnreachable { get; set; }

    public Task<List<SurveyTemplate>?> GetTemplatesAsync() =>
        Task.FromResult<List<SurveyTemplate>?>(Unreachable || TemplatesUnreachable ? null : Templates.ToList());

    public List<Guid> UsedTemplates { get; } = new();

    public Task<(SurveyDefinition? Survey, string? Error)> UseTemplateAsync(Guid templateId)
    {
        var template = Templates.FirstOrDefault(t => t.Id == templateId);
        if (NextCopyError is not null || template is null)
        {
            return Task.FromResult<(SurveyDefinition?, string?)>((null, NextCopyError ?? "Template not found."));
        }
        UsedTemplates.Add(templateId);
        return Task.FromResult<(SurveyDefinition?, string?)>((AddCopy(templateId, template.Title, [], []), null));
    }

    private SurveyDefinition AddCopy(Guid sourceId, string title, List<Guid> questionIds, List<Guid> pageBreaks)
    {
        var copy = new SurveyDefinition
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"A copy of {sourceId}",
            QuestionIds = questionIds.ToList(),
            PageBreaks = pageBreaks.ToList(),
            CreatedAt = DateTime.UtcNow,
            Status = SurveyStatuses.Draft,
            Listed = false,
        };
        Surveys.Add(copy);
        if (Questions.TryGetValue(sourceId, out var questions))
        {
            Questions[copy.Id] = questions.ToList();
        }
        return copy;
    }

    public string? NextSharingError { get; set; }
    public (Guid Id, SurveySharing Sharing)? LastSharing { get; private set; }

    /// <summary>Applies the settings to the stored survey, like the API does.</summary>
    public Task<(SurveyDefinition? Survey, string? Error)> UpdateSharingAsync(Guid id, SurveySharing sharing)
    {
        LastSharing = (id, sharing);
        var survey = Surveys.FirstOrDefault(s => s.Id == id);
        if (NextSharingError is not null || survey is null)
        {
            return Task.FromResult<(SurveyDefinition?, string?)>((null, NextSharingError ?? "Survey not found."));
        }
        survey.Status = sharing.Status;
        survey.Listed = sharing.Listed;
        survey.ClosesAt = sharing.ClosesAt;
        return Task.FromResult<(SurveyDefinition?, string?)>((survey, null));
    }

    public string? LastRespondentId { get; private set; }

    public Task<SubmitResult> SubmitResponseAsync(Guid surveyId, Dictionary<string, List<string>> answers, string? respondentId = null)
    {
        LastSubmitted = answers.ToDictionary(a => a.Key, a => a.Value.ToList());
        LastRespondentId = respondentId;
        return Task.FromResult(NextSubmitResult);
    }

    /// <summary>Respondent ids that have already answered, per survey.</summary>
    public HashSet<(Guid SurveyId, string RespondentId)> Answered { get; } = new();

    public Task<bool> HasAnsweredAsync(Guid surveyId, string respondentId) =>
        Task.FromResult(Answered.Contains((surveyId, respondentId)));

    /// <summary>Every query the results page asked for, in order.</summary>
    public List<ResultsQuery?> ResultsQueries { get; } = new();

    /// <summary>Answers each results request; defaults to returning <see cref="Results"/>.</summary>
    public Func<ResultsQuery?, SurveyResults?>? ResultsFor { get; set; }

    public Task<SurveyResults?> GetResultsAsync(Guid surveyId, ResultsQuery? query = null)
    {
        ResultsQueries.Add(query);
        return Task.FromResult(ResultsFor is null ? Results : ResultsFor(query));
    }

    public ResultsQuery? LastExportQuery { get; private set; }

    public CsvExport? Export { get; set; }

    public Task<CsvExport?> ExportResponsesAsync(Guid surveyId, ResultsQuery? query = null)
    {
        LastExportQuery = query;
        return Task.FromResult(Export);
    }
}
