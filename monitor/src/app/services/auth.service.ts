import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject } from 'rxjs';
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
  readonly stateChanged = new BehaviorSubject<void>(undefined);
  private apiUrl = `${environment.apiUrl}/Auth`;

  constructor(private http: HttpClient) {}

  serverCapabilities(): Observable<{environment:string;developmentToolsAvailable:boolean}> { return this.http.get<{environment:string;developmentToolsAvailable:boolean}>(`${environment.apiUrl}/system/capabilities`); }

  login(login: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, { login, password }).pipe(
      tap(res => {
        if (res.succeeded && res.response?.accessToken) {
          localStorage.setItem('access_token', res.response.accessToken);
          localStorage.setItem('user_login', res.response.login);
          localStorage.setItem('user_role', res.response.role);
          this.stateChanged.next();
        }
      })
    );
  }

  refresh(): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/refresh`, {}, {withCredentials: true}).pipe(tap(res => {
      localStorage.setItem("access_token",res.response.accessToken);
      localStorage.setItem("user_login",res.response.login);
      localStorage.setItem("user_role",res.response.role);
      this.stateChanged.next();
    }));
  }

  logout(): void {
    this.http.post(`${this.apiUrl}/logout`,{}, {withCredentials: true}).subscribe({error: () => {}});
    localStorage.removeItem('access_token');
    localStorage.removeItem('user_login');
    localStorage.removeItem('user_role');
    this.stateChanged.next();
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  getLogin(): string | null {
    return localStorage.getItem('user_login');
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
    } catch { return false; }
  }
}
