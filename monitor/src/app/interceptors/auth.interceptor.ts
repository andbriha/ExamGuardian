import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, finalize, shareReplay, switchMap, throwError } from 'rxjs';
import { AuthService, LoginResponse } from '../services/auth.service';
let refreshPending: Observable<LoginResponse> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Attach credentials only to this application's API, never to external evidence URLs.
  if (!req.url.startsWith('/api/')) return next(req);
  const token = localStorage.getItem('access_token');
  const authReq = token ? req.clone({setHeaders: {Authorization: `Bearer ${token}`}}) : req;
  if (req.url.includes('/Auth/')) return next(authReq);
  const auth = inject(AuthService);
  return next(authReq).pipe(catchError(error => {
    if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !token) return throwError(() => error);
    refreshPending ??= auth.refresh().pipe(finalize(() => refreshPending = null), shareReplay({bufferSize: 1,refCount: false}));
    return refreshPending.pipe(switchMap(login => next(req.clone({setHeaders: {Authorization: `Bearer ${login.response.accessToken}`}}))));
  }));
};
