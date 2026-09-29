/** Stable permission codes. UI and guards check these strings — never role names. */
export const PermissionCodes = {
  UsersManage: 'users.manage',
  RolesManage: 'roles.manage',
  ArchiveRestoreResumes: 'archive.restore.resumes',
  ArchiveRestoreAttempts: 'archive.restore.attempts',
  ArchiveRestoreCatalog: 'archive.restore.catalog',
  OpeningsRead: 'openings.read',
  OpeningsWrite: 'openings.write',
  OpeningsFieldsManage: 'openings.fields.manage',
  QuizzesRead: 'quizzes.read',
  QuizzesWrite: 'quizzes.write',
  TemplatesRead: 'templates.read',
  TemplatesWrite: 'templates.write',
  AiRulesManage: 'ai.rules.manage',
  AiDraftUse: 'ai.draft.use',
  AssignmentsRead: 'assignments.read',
  AssignmentsWrite: 'assignments.write',
  SessionsLiveRun: 'sessions.live.run',
  AttemptsRead: 'attempts.read',
  AttemptsReview: 'attempts.review',
  FiltersWrite: 'filters.write',
  FiltersShare: 'filters.share',
  CandidateAttemptParticipate: 'candidate.attempt.participate',
} as const;

export type PermissionCode = (typeof PermissionCodes)[keyof typeof PermissionCodes];
