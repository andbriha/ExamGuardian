import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LoginResponse {
  succeeded: boolean;
  response: {
    accessToken: string;
    expiresAt: string;
    login: string;
    role: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = `${environment.apiUrl}/Auth`;

  constructor(private http: HttpClient) {}

  login(login: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, { login, password }).pipe(
      tap(res => {
        if (res.succeeded && res.response?.accessToken) {
          localStorage.setItem('access_token', res.response.accessToken);
          localStorage.setItem('user_login', res.response.login);
          localStorage.setItem('user_role', res.response.role);
        }
      })
    );
  }

  logout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('user_login');
    localStorage.removeItem('user_role');
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  getLogin(): string | null {
    return localStorage.getItem('user_login');
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }
}
