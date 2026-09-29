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
  startDate: string;
  expectedCloseDate: string | null;
  headcount: number;
  expectedExperienceYears: number;
  handlers: string[];
  tags: Record<string, string>;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
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
}

export interface QuizResponse {
  id: string;
  openingId: string;
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: Record<string, string>;
  questions: QuestionResponse[];
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface QuestionRequest {
  id?: string;
  type: QuestionType;
  stem: string;
  scoringMode?: ScoringMode;
  creditMode?: CreditMode;
  points: number;
  body: QuestionBody;
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

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  [key: string]: unknown;
}
