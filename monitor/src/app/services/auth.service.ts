import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject, of, map, catchError, finalize, shareReplay, throwError } from 'rxjs';
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

  private accessToken: string | null = null;
  private loginName: string | null = null;
  private role: string | null = null;
  private refreshPending: Observable<LoginResponse> | null = null;
  private epoch = 0;
  constructor(private http: HttpClient) {
    // Remove legacy persistent credentials rather than restoring them into the new session.
    for (const key of ['access_token','user_login','user_role']) localStorage.removeItem(key);
  }
  private accept(res: LoginResponse): void {
    if (!res.succeeded || !res.response?.accessToken) throw new Error('Invalid authentication response');
    this.accessToken = res.response.accessToken; this.loginName = res.response.login; this.role = res.response.role;
    this.stateChanged.next();
  }
  private clear(): void { this.accessToken = null; this.loginName = null; this.role = null; this.stateChanged.next(); }
  ensureAuthenticated(): Observable<boolean> {
    return this.isAuthenticated() ? of(true) : this.refresh().pipe(map(() => this.isAuthenticated()), catchError(() => of(false)));
  }
  getRole(): string | null { return this.role; }

  serverCapabilities(): Observable<{environment:string;developmentToolsAvailable:boolean}> { return this.http.get<{environment:string;developmentToolsAvailable:boolean}>(`${environment.apiUrl}/system/capabilities`); }

  login(login: string, password: string): Observable<LoginResponse> {
    const epoch = ++this.epoch;
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, {login,password}, {withCredentials:true}).pipe(tap(res => {
      if (epoch !== this.epoch) throw new Error('Session changed');
      this.accept(res);
    }));
  }
  refresh(): Observable<LoginResponse> {
    if (!this.refreshPending) {
      const epoch = this.epoch;
      this.refreshPending = this.http.post<LoginResponse>(`${this.apiUrl}/refresh`, {}, {withCredentials:true}).pipe(
        tap(res => { if (epoch !== this.epoch) throw new Error('Session changed'); this.accept(res); }),
        catchError(error => { if (epoch === this.epoch) this.clear(); return throwError(() => error); }),
        finalize(() => this.refreshPending = null), shareReplay({bufferSize:1,refCount:false}));
    }
    return this.refreshPending;
  }
  logout(): void {
    ++this.epoch;
    this.http.post(`${this.apiUrl}/logout`, {}, {withCredentials:true}).subscribe({error: () => {}});
    this.clear();
  }
  getToken(): string | null { return this.accessToken; }
  getLogin(): string | null { return this.loginName; }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
    } catch { return false; }
  }
}
