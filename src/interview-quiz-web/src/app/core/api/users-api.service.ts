import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AssignableUser } from './contracts';

@Injectable({
  providedIn: 'root',
})
export class UsersApi {
  private readonly http = inject(HttpClient);

  searchAssignable(query: string, limit = 20): Observable<AssignableUser[]> {
    let params = new HttpParams().set('limit', limit);
    const q = query.trim();
    if (q) {
      params = params.set('q', q);
    }
    return this.http.get<AssignableUser[]>('/api/users/assignable', { params });
  }
}
