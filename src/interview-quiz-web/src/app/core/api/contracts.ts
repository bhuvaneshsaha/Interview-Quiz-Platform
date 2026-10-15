/** Typed contracts matching the Interview Quiz API (camelCase JSON). */

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  tokenType: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface MeResponse {
  id: string;
  email: string;
  permissions: string[];
}

export interface PermissionResponse {
  code: string;
  displayName: string;
  module: string;
  includeInEmployeeRoleEditor: boolean;
}

export interface RoleResponse {
  id: string;
  name: string;
  description: string | null;
  permissionCodes: string[];
  userIds: string[];
  isSystem: boolean;
}

export interface CreateRoleRequest {
  name: string;
  description: string | null;
  permissionCodes: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description: string | null;
  permissionCodes: string[];
}

export interface AssignRoleRequest {
  userId: string;
}

export interface UserResponse {
  id: string;
  email: string;
  isDisabled: boolean;
  roleIds: string[];
}

export interface CreateUserRequest {
  email: string;
  password: string;
}

export interface UpdateUserRequest {
  isDisabled: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface OpeningResponse {
  id: string;
  title: string;
  jobDescription: string;
  owner: string;
  ownerName?: string;
  ownerEmail?: string;
  ownerResolved?: boolean;
  startDate: string;
  expectedCloseDate: string | null;
  headcount: number;
  expectedExperienceYears: number;
  handlers: string[];
  handlerPeople?: OpeningPerson[];
  tags: Record<string, string>;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface OpeningPerson {
  userId: string;
  name: string;
  email: string;
  resolved: boolean;
}

export interface AssignableUser {
  id: string;
  name: string;
  email: string;
}

export interface CreateOpeningRequest {
  title: string;
  jobDescription: string;
  owner: string;
  startDate: string;
  expectedCloseDate: string | null;
  headcount: number;
  expectedExperienceYears: number;
  handlers: string[];
  tags: Record<string, string>;
}

export interface UpdateOpeningRequest extends CreateOpeningRequest {
  id: string;
  rowVersion: number;
}

export interface OpeningFieldDefinitionResponse {
  id: string;
  key: string;
  displayName: string;
  sortOrder: number;
}

export interface OpeningFieldDefinitionDto {
  key: string;
  displayName: string;
  sortOrder: number;
}

export interface ReplaceOpeningFieldDefinitionsRequest {
  items: OpeningFieldDefinitionDto[];
}

export type QuestionType =
  | 'multipleChoiceSingle'
  | 'multipleChoiceMulti'
  | 'trueFalse'
  | 'shortText'
  | 'longText'
  | 'dragDropSharedBank'
  | 'dragDropPerSlot'
  | 'ordering';

export type ScoringMode = 'auto' | 'aiAssist' | 'humanOnly';

export type CreditMode = 'partial' | 'allOrNothing';

export interface ChoiceOptionBody {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface MultipleChoiceBody {
  options: ChoiceOptionBody[];
}

export interface TrueFalseBody {
  correct: boolean;
}

export interface ShortTextBody {
  acceptableAnswers: string[];
  caseSensitive: boolean;
}

export interface LongTextBody {
  maxLength?: number;
  guidance?: string;
}

export interface SharedBankSlotBody {
  id: string;
  label: string;
  correctItemId: string;
}

export interface BankItemBody {
  id: string;
  text: string;
  isDistractor: boolean;
}

export interface DragDropSharedBankBody {
  slots: SharedBankSlotBody[];
  bank: BankItemBody[];
}

export interface PerSlotBody {
  id: string;
  label: string;
  options: ChoiceOptionBody[];
}

export interface DragDropPerSlotBody {
  slots: PerSlotBody[];
}

export interface OrderingItemBody {
  id: string;
  text: string;
  correctIndex: number;
}

export interface OrderingBody {
  items: OrderingItemBody[];
}

export type QuestionBody =
  | MultipleChoiceBody
  | TrueFalseBody
  | ShortTextBody
  | LongTextBody
  | DragDropSharedBankBody
  | DragDropPerSlotBody
  | OrderingBody;

export interface QuestionResponse {
  id: string;
  sortOrder: number;
  type: QuestionType;
  stem: string;
  scoringMode: ScoringMode;
  creditMode: CreditMode | null;
  points: number;
  body: QuestionBody;
  sourceQuestionId: string | null;
}

export interface QuizResponse {
  id: string;
  openingId: string;
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  questions: QuestionResponse[];
  originTemplateId: string | null;
  sourceTemplateVersionId: string | null;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  assignmentCount?: number;
}

export interface QuestionRequest {
  id?: string;
  type: QuestionType;
  stem: string;
  scoringMode?: ScoringMode;
  creditMode?: CreditMode;
  points: number;
  body: QuestionBody;
  sourceQuestionId?: string | null;
}

export interface BankQuestionResponse {
  id: string;
  title: string;
  tags: Record<string, string>;
  expectedExperienceYears: number;
  type: QuestionType;
  stem: string;
  scoringMode: ScoringMode;
  creditMode: CreditMode | null;
  points: number;
  body: QuestionBody;
  archivedAtUtc: string | null;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateBankQuestionRequest {
  title: string;
  tags: Record<string, string>;
  expectedExperienceYears: number;
  type: QuestionType;
  stem: string;
  scoringMode?: ScoringMode;
  creditMode?: CreditMode;
  points: number;
  body: QuestionBody;
}

export interface UpdateBankQuestionRequest extends CreateBankQuestionRequest {
  rowVersion: number;
}

export interface BankQuestionListCriteria {
  keyword?: string;
  type?: QuestionType;
  experienceMinYears?: number;
  experienceMaxYears?: number;
  tags?: Record<string, string>;
  archived?: boolean;
}

export interface IncludeQuestionsRequest {
  questionIds: string[];
  insertAt?: number | null;
  rowVersion: number;
}

export interface CreateQuizRequest {
  openingId: string;
  title: string;
  description?: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  questions: QuestionRequest[];
}

export interface UpdateQuizRequest extends CreateQuizRequest {
  id: string;
  rowVersion: number;
}

export interface OpeningListCriteria {
  owner?: string;
  experienceMinYears?: number;
  experienceMaxYears?: number;
  startDateFrom?: string;
  startDateTo?: string;
  expectedCloseDateFrom?: string;
  expectedCloseDateTo?: string;
  tags?: Record<string, string>;
}

export interface QuizListCriteria {
  openingId?: string;
  keyword?: string;
  experienceMinYears?: number;
  experienceMaxYears?: number;
  tags?: Record<string, string>;
}

export interface TemplateListCriteria {
  keyword?: string;
  experienceMinYears?: number;
  experienceMaxYears?: number;
  tags?: Record<string, string>;
}

export type ReportFilterValue =
  | string
  | number
  | string[]
  | number[]
  | { from: string | number; to: string | number };

export interface ReportFilterChip {
  field: string;
  operator: string;
  value: ReportFilterValue;
}

export interface ReportFilterCriteria {
  filters?: ReportFilterChip[];
}

export type ListCriteria =
  | OpeningListCriteria
  | QuizListCriteria
  | TemplateListCriteria
  | ReportFilterCriteria;

export type FilterTarget =
  | 'openings'
  | 'quizzes'
  | 'templates'
  | 'reportsFunnel'
  | 'reportsScores'
  | 'reportsTime'
  | 'reportsQuestions'
  | 'reportsCandidates'
  | 'reportsPendingReview';

export type FilterShareMode = 'private' | 'publicInsideCompany' | 'specificUsers';

export interface FilterResponse {
  id: string;
  name: string;
  target: FilterTarget;
  criteria: ListCriteria;
  ownerUserId: string;
  shareMode: FilterShareMode;
  sharedWithUserIds: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateFilterRequest {
  name: string;
  target: FilterTarget;
  criteria: ListCriteria;
}

export interface UpdateFilterRequest {
  name: string;
  target: FilterTarget;
  criteria: ListCriteria;
}

export interface ShareFilterRequest {
  shareMode: 'publicInsideCompany' | 'specificUsers';
  userIds?: string[];
}

export interface TemplateSummaryResponse {
  id: string;
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  latestVersionId: string;
  latestVersionNumber: number;
  originQuizId: string;
  updatedAtUtc: string;
  rowVersion: number;
}

export interface TemplateResponse {
  id: string;
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  latestVersionId: string;
  latestVersionNumber: number;
  originQuizId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number;
}

export interface UpdateTemplateMetadataRequest {
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  rowVersion: number;
  changeNote: string;
}

export interface SaveTemplateContentRequest extends UpdateTemplateMetadataRequest {
  questions: QuestionRequest[];
}

export interface TemplateVersionSummaryResponse {
  id: string;
  templateId: string;
  versionNumber: number;
  title: string;
  publishedFromQuizId: string;
  publishedAtUtc: string;
  publishedByUserId: string;
  publishedByDisplayName: string;
  changeNote: string;
  questionCount: number;
}

export interface TemplateVersionResponse extends TemplateVersionSummaryResponse {
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  questions: QuestionResponse[];
}

export interface PublishTemplateResponse {
  templateId: string;
  versionId: string;
  versionNumber: number;
  createdNewTemplate: boolean;
}

export interface CloneTemplateVersionRequest {
  openingId: string;
  title?: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  [key: string]: unknown;
}

export type AssignmentMode = 'async' | 'live';

export type AssignmentStatus = 'notStarted' | 'inProgress' | 'pendingReview' | 'completed';

export interface AssignmentListQuery {
  openingId?: string;
  quizId?: string;
  keyword?: string;
}

export interface OpeningCandidateCounts {
  assigned: number;
  appeared: number;
  submitted: number;
}

export interface OpeningQuizSummary {
  id: string;
  title: string | null;
  questionCount: number | null;
  updatedAtUtc: string | null;
  candidates: OpeningCandidateCounts | null;
}

export interface OpeningSummaryResponse {
  openingId: string;
  quizzes: OpeningQuizSummary[] | null;
  candidates: OpeningCandidateCounts | null;
}

export interface AssignmentTimingRequest {
  overallDurationMinutes?: number | null;
}

export interface CreateAssignmentRequest {
  openingId: string;
  quizId: string;
  candidateEmail: string;
  mode: AssignmentMode;
  timing?: AssignmentTimingRequest | null;
  attemptLimit?: number | null;
  inviteExpiresAt?: string | null;
}

export interface CreateAssignmentFromTemplateRequest {
  openingId: string;
  templateId: string;
  templateVersionId?: string | null;
  candidateEmail: string;
  mode: AssignmentMode;
  timing?: AssignmentTimingRequest | null;
  attemptLimit?: number | null;
  quizTitle?: string | null;
  inviteExpiresAt?: string | null;
}

export interface AssignmentSummaryResponse {
  id: string;
  openingId: string;
  quizId: string;
  snapshotId: string;
  snapshotTitle: string;
  snapshotQuestionCount: number;
  snapshotQuizRowVersion: number;
  candidateEmail: string;
  mode: AssignmentMode | string;
  overallDurationMinutes: number | null;
  attemptLimit: number;
  status: AssignmentStatus | string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export type InviteEmailStatus = 'none' | 'sent' | 'failed' | 'resent' | string;
export type InviteState = 'none' | 'active' | 'expired' | 'revoked' | string;

export interface AssignmentResponse extends AssignmentSummaryResponse {
  createdByUserId: string;
  inviteUrl?: string | null;
  inviteEmailStatus?: InviteEmailStatus;
  inviteEmailAttemptCount?: number;
  inviteEmailLastAttemptAt?: string | null;
  inviteEmailLastError?: string | null;
  inviteState?: InviteState;
  inviteExpiresAt?: string | null;
}

export interface InviteResponse {
  assignmentId: string;
  inviteUrl: string;
  expiresAt?: string | null;
  inviteEmailStatus?: InviteEmailStatus;
  inviteEmailAttemptCount?: number;
  inviteEmailLastError?: string | null;
}

export interface InviteLifetimeResponse {
  assignmentId: string;
  inviteState: string;
  expiresAt?: string | null;
}

export interface InviteDeliveryResponse {
  assignmentId: string;
  inviteEmailStatus: InviteEmailStatus;
  inviteEmailAttemptCount: number;
  inviteEmailLastAttemptAt?: string | null;
  inviteEmailLastError?: string | null;
}

export interface ConsumeMagicLinkRequest {
  token: string;
}

export interface CandidateTokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  tokenType: string;
  assignmentId: string;
}

export interface MultipleChoiceSingleAnswer {
  optionId: string;
}

export interface MultipleChoiceMultiAnswer {
  optionIds: string[];
}

export interface TrueFalseAnswer {
  value: boolean;
}

export interface TextAnswer {
  text: string;
}

export interface DragDropSharedSlotAnswer {
  slotId: string;
  itemId: string;
}

export interface DragDropPerSlotAnswer {
  slotId: string;
  optionId: string;
}

export interface DragDropAnswer {
  slots: Array<DragDropSharedSlotAnswer | DragDropPerSlotAnswer>;
}

export interface OrderingAnswer {
  itemIds: string[];
}

export type AnswerValue =
  | MultipleChoiceSingleAnswer
  | MultipleChoiceMultiAnswer
  | TrueFalseAnswer
  | TextAnswer
  | DragDropAnswer
  | OrderingAnswer
  | Record<string, unknown>;

export interface AnswerDto {
  questionId: string;
  value: AnswerValue;
}

export interface SaveAnswersRequest {
  answers: AnswerDto[];
}

export interface CandidateQuestion {
  id: string;
  sortOrder: number;
  type: QuestionType;
  stem: string;
  body: Record<string, unknown>;
  sourceQuestionId: string | null;
}

export interface CandidateItemResult {
  questionId: string;
  status: 'scored' | 'unsettled' | string;
  pointsAwarded: number | null;
}

export interface CandidateQuizPreviewResponse {
  title: string;
  openingName: string;
  questionCount: number;
  suggestedDurationMinutes: number;
  rowVersion: number;
  questions: CandidateQuestion[];
}

export interface CandidatePreviewScoreItem {
  questionId: string;
  status: string;
  pointsAwarded: number | null;
  points: number;
}

export interface CandidatePreviewScoreResponse {
  autoPointsAwarded: number;
  autoPointsAvailable: number;
  items: CandidatePreviewScoreItem[];
}

export type LiveCandidateState = 'waiting' | 'running' | 'paused' | 'ended' | 'submitted' | string;

export interface CandidateAttemptPreview {
  attemptStatus: 'notStarted' | 'inProgress' | 'submitted' | string;
  quizTitle: string;
  openingName: string;
  durationMinutes: number;
  questionCount: number;
  liveState?: LiveCandidateState | null;
}

export interface CandidateAttemptResponse {
  id: string;
  assignmentId: string;
  status: string;
  startedAtUtc: string;
  dueAtUtc: string;
  submittedAtUtc: string | null;
  remainingSeconds: number;
  questions: CandidateQuestion[];
  answers: AnswerDto[];
  itemResults?: CandidateItemResult[] | null;
  flaggedQuestionIds?: string[];
  liveState?: LiveCandidateState | null;
  paused?: boolean;
  liveEndReason?: string | null;
}

export interface LiveProgressResponse {
  assignmentId: string;
  attemptId?: string | null;
  state: string;
  candidateConnected: boolean;
  lastActivityAtUtc?: string | null;
  currentQuestionIndex?: number | null;
  questionCount: number;
  answeredCount: number;
  flaggedCount: number;
  remainingSeconds?: number | null;
  startedAtUtc?: string | null;
  pausedAtUtc?: string | null;
  totalPausedSeconds: number;
  endedAtUtc?: string | null;
  endReason?: string | null;
}

export interface ReviewFlagDto {
  questionId: string;
  flagged: boolean;
}

export interface SetReviewFlagsRequest {
  flags: ReviewFlagDto[];
}

export interface ReportPositionRequest {
  questionIndex: number;
}

export interface CandidateSubmitResponse {
  id: string;
  assignmentId: string;
  status: string;
  resultStatus: string;
  startedAtUtc: string;
  dueAtUtc: string;
  submittedAtUtc: string | null;
  autoPointsAwarded: number;
  autoPointsAvailable: number;
  totalPointsAvailable: number;
  itemResults: CandidateItemResult[];
}

export interface AttemptSummaryResponse {
  id: string;
  assignmentId: string;
  openingId: string;
  candidateEmail: string;
  status: string;
  resultStatus: string;
  startedAtUtc: string;
  submittedAtUtc: string | null;
  autoPointsAwarded: number;
  autoPointsAvailable: number;
  totalPointsAvailable: number;
  quizRowVersion?: number;
}

export interface AttemptResultItem {
  questionId: string;
  sortOrder: number;
  type: string;
  scoringMode: string;
  points: number;
  status: string;
  pointsAwarded: number | null;
  candidateAnswer?: unknown;
  stem?: string | null;
  /** Redacted snapshot body used to label option and item ids. */
  body?: unknown;
}

export interface AttemptResultResponse extends AttemptSummaryResponse {
  items: AttemptResultItem[];
}

export interface ReportBucket {
  label: string;
  count: number;
}

export interface ReportWindow {
  defaultWindowApplied: boolean;
  appliedCreatedFromUtc?: string | null;
  appliedCreatedToUtc?: string | null;
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface FunnelReportRow {
  openingId?: string | null;
  openingTitle: string;
  quizId?: string | null;
  quizTitle: string;
  assigned: number;
  appeared: number;
  submitted: number;
  completed: number;
  pendingReview: number;
  appearedRate: number;
  submittedRate: number;
  completedRate: number;
}

export interface FunnelReportResponse extends ReportWindow {
  totals: FunnelReportRow;
  items: FunnelReportRow[];
}

export interface ScoreReportRow {
  attemptId: string;
  assignmentId: string;
  openingId: string;
  openingTitle: string;
  quizId: string;
  quizTitle: string;
  candidateEmail: string;
  mode: string;
  status: string;
  resultStatus?: string | null;
  autoPointsAwarded: number;
  autoPointsAvailable: number;
  totalPointsAvailable: number;
  unsettledCount: number;
  passed?: boolean | null;
  snapshotId: string;
  quizRowVersion: number;
}

export interface ScoresReportResponse extends ReportWindow {
  passThreshold: number;
  submittedCount: number;
  passDenominator: number;
  passCount: number;
  passRate: number;
  histogram: ReportBucket[];
  items: ScoreReportRow[];
}

export interface TimeReportRow {
  attemptId: string;
  assignmentId: string;
  openingId: string;
  openingTitle: string;
  quizId: string;
  quizTitle: string;
  candidateEmail: string;
  mode: string;
  durationSeconds: number;
  pauseTracked: boolean;
  snapshotId: string;
  quizRowVersion: number;
}

export interface TimeReportResponse extends ReportWindow {
  p50Seconds?: number | null;
  p90Seconds?: number | null;
  histogram: ReportBucket[];
  items: TimeReportRow[];
}

export interface QuestionReportRow {
  questionId: string;
  sortOrder: number;
  stem: string;
  openingId: string;
  openingTitle: string;
  quizId: string;
  quizTitle: string;
  settledCount: number;
  difficulty: number;
  discrimination?: number | null;
  discriminationHidden: boolean;
  snapshotCount: number;
}

export interface QuestionsReportResponse extends ReportWindow {
  items: QuestionReportRow[];
}

export interface CandidateReportRow {
  rank: number;
  candidateEmail: string;
  status: string;
  autoPointsAwarded?: number | null;
  autoPointsAvailable?: number | null;
  durationSeconds?: number | null;
  mode: string;
  snapshotId: string;
  quizRowVersion: number;
  assignmentId: string;
  attemptId?: string | null;
  openingId: string;
  openingTitle: string;
  quizId: string;
  quizTitle: string;
}

export interface CandidatesReportResponse extends ReportWindow {
  items: CandidateReportRow[];
}

export interface PendingReviewReportRow {
  assignmentId: string;
  attemptId?: string | null;
  openingId: string;
  openingTitle: string;
  quizId: string;
  quizTitle: string;
  candidateEmail: string;
  mode: string;
  submittedAtUtc?: string | null;
  ageHours: number;
  unsettledCount: number;
  snapshotId: string;
  quizRowVersion: number;
}

export interface PendingReviewReportResponse extends ReportWindow {
  items: PendingReviewReportRow[];
}

export type ReportResponse =
  | FunnelReportResponse
  | ScoresReportResponse
  | TimeReportResponse
  | QuestionsReportResponse
  | CandidatesReportResponse
  | PendingReviewReportResponse;
