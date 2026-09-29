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

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  [key: string]: unknown;
}
