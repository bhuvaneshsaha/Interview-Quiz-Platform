namespace InterviewQuiz.Kernel.Permissions;

/// <summary>
/// Stable app-defined permission codes. Operators compose roles from these;
/// API and UI must check codes, never role names.
/// </summary>
public static class PermissionCodes
{
    public static class Access
    {
        public const string UsersManage = "users.manage";
        public const string RolesManage = "roles.manage";
        public const string ArchiveRestoreResumes = "archive.restore.resumes";
        public const string ArchiveRestoreAttempts = "archive.restore.attempts";
        public const string ArchiveRestoreCatalog = "archive.restore.catalog";
    }

    public static class Openings
    {
        public const string Read = "openings.read";
        public const string Write = "openings.write";
        public const string FieldsManage = "openings.fields.manage";
    }

    public static class Catalog
    {
        public const string QuizzesRead = "quizzes.read";
        public const string QuizzesWrite = "quizzes.write";
        public const string TemplatesRead = "templates.read";
        public const string TemplatesWrite = "templates.write";
        public const string QuestionsRead = "questions.read";
        public const string QuestionsWrite = "questions.write";
        public const string AiRulesManage = "ai.rules.manage";
        public const string AiDraftUse = "ai.draft.use";
    }

    public static class Delivery
    {
        public const string AssignmentsRead = "assignments.read";
        public const string AssignmentsWrite = "assignments.write";
        public const string SessionsLiveRun = "sessions.live.run";
    }

    public static class Evaluation
    {
        public const string AttemptsRead = "attempts.read";
        public const string AttemptsReview = "attempts.review";
    }

    public static class Search
    {
        public const string FiltersWrite = "filters.write";
        public const string FiltersShare = "filters.share";
    }

    public static class Candidate
    {
        public const string AttemptParticipate = "candidate.attempt.participate";
    }

    public static IReadOnlyList<PermissionDescriptor> All { get; } =
    [
        new(Access.UsersManage, "Manage users", "access"),
        new(Access.RolesManage, "Manage permission groups", "access"),
        new(Access.ArchiveRestoreResumes, "Restore archived resumes", "access"),
        new(Access.ArchiveRestoreAttempts, "Restore archived attempts", "access"),
        new(Access.ArchiveRestoreCatalog, "Restore archived quiz content", "access"),
        new(Openings.Read, "View openings", "openings"),
        new(Openings.Write, "Create and edit openings", "openings"),
        new(Openings.FieldsManage, "Manage opening field defaults", "openings"),
        new(Catalog.QuizzesRead, "View quizzes", "catalog"),
        new(Catalog.QuizzesWrite, "Create and edit quizzes", "catalog"),
        new(Catalog.TemplatesRead, "View templates", "catalog"),
        new(Catalog.TemplatesWrite, "Create and edit templates", "catalog"),
        new(Catalog.QuestionsRead, "View question bank", "catalog"),
        new(Catalog.QuestionsWrite, "Create and edit question bank", "catalog"),
        new(Catalog.AiRulesManage, "Manage company AI rule sets", "catalog"),
        new(Catalog.AiDraftUse, "Use AI draft", "catalog"),
        new(Delivery.AssignmentsRead, "View assignments", "delivery"),
        new(Delivery.AssignmentsWrite, "Assign quizzes", "delivery"),
        new(Delivery.SessionsLiveRun, "Run live sessions", "delivery"),
        new(Evaluation.AttemptsRead, "View attempts and results", "evaluation"),
        new(Evaluation.AttemptsReview, "Review attempts", "evaluation"),
        new(Search.FiltersWrite, "Manage own saved filters", "search"),
        new(Search.FiltersShare, "Share saved filters", "search"),
        new(Candidate.AttemptParticipate, "Take and submit own attempt", "candidate", IncludeInEmployeeRoleEditor: false)
    ];
}
