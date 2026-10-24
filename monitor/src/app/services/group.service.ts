import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class GroupService {
  private url = `${environment.apiUrl}/groups`;
  constructor(private http: HttpClient) {}
  getAll() { return this.http.get<string[]>(this.url); }
  create(name: string) { return this.http.post<{name: string}>(this.url, { name }); }
  assign(studentId: string, name: string) { return this.http.put<void>(`${this.url}/students/${studentId}`, { name }); }
}
