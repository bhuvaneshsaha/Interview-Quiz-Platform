import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignRoleRequest,
  CreateRoleRequest,
  CreateUserRequest,
  PagedResult,
  PermissionResponse,
  RoleResponse,
  UpdateRoleRequest,
  UpdateUserRequest,
  UserResponse,
} from './contracts';

@Injectable({
  providedIn: 'root',
})
export class AccessApi {
  private readonly http = inject(HttpClient);

  listPermissions(): Observable<PermissionResponse[]> {
    return this.http.get<PermissionResponse[]>('/api/permissions');
  }

  listRoles(): Observable<RoleResponse[]> {
    return this.http.get<RoleResponse[]>('/api/roles');
  }

  getRole(id: string): Observable<RoleResponse> {
    return this.http.get<RoleResponse>(`/api/roles/${id}`);
  }

  createRole(body: CreateRoleRequest): Observable<RoleResponse> {
    return this.http.post<RoleResponse>('/api/roles', body);
  }

  updateRole(id: string, body: UpdateRoleRequest): Observable<RoleResponse> {
    return this.http.put<RoleResponse>(`/api/roles/${id}`, body);
  }

  deleteRole(id: string): Observable<void> {
    return this.http.delete<void>(`/api/roles/${id}`);
  }

  assignRole(roleId: string, userId: string): Observable<void> {
    const body: AssignRoleRequest = { userId };
    return this.http.post<void>(`/api/roles/${roleId}/users`, body);
  }

  unassignRole(roleId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/roles/${roleId}/users/${userId}`);
  }

  listUsers(page = 1, pageSize = 20): Observable<PagedResult<UserResponse>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<UserResponse>>('/api/users', { params });
  }

  getUser(id: string): Observable<UserResponse> {
    return this.http.get<UserResponse>(`/api/users/${id}`);
  }

  createUser(body: CreateUserRequest): Observable<UserResponse> {
    return this.http.post<UserResponse>('/api/users', body);
  }

  updateUser(id: string, body: UpdateUserRequest): Observable<UserResponse> {
    return this.http.put<UserResponse>(`/api/users/${id}`, body);
  }
}
