import {provideZonelessChangeDetection} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController,provideHttpClientTesting} from '@angular/common/http/testing';
import {AuthService} from './auth.service';

describe('Authentication session', () => {
  let service: AuthService; let http: HttpTestingController;
  const reply={succeeded:true,response:{accessToken:'x.'+btoa(JSON.stringify({exp:4102444800}))+'.x',login:'teacher',role:'Examiner',expiresAt:'2100-01-01'}};
  beforeEach(() => {TestBed.configureTestingModule({providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]});service=TestBed.inject(AuthService);http=TestBed.inject(HttpTestingController);});
  afterEach(() => http.verify());
  it('keeps access tokens only in memory', () => {
    service.login('teacher','password').subscribe();http.expectOne('/api/Auth/login').flush(reply);
    expect(service.getToken()).toBe(reply.response.accessToken);expect(localStorage.getItem('access_token')).toBeNull();
  });
  it('shares refresh requests and restores the role after reload', () => {
    service.ensureAuthenticated().subscribe();service.ensureAuthenticated().subscribe();
    http.expectOne('/api/Auth/refresh').flush(reply);expect(service.getRole()).toBe('Examiner');expect(service.isAuthenticated()).toBeTrue();
  });
  it('cannot restore a session from a late refresh after logout', () => {
    service.refresh().subscribe({error:()=>{}});const refresh=http.expectOne('/api/Auth/refresh');
    service.logout();http.expectOne('/api/Auth/logout').flush(null);refresh.flush(reply);
    expect(service.getToken()).toBeNull();
  });
  it('clears local identity when refresh is rejected', () => {
    service.login('teacher','password').subscribe();http.expectOne('/api/Auth/login').flush(reply);
    service.refresh().subscribe({error:()=>{}});http.expectOne('/api/Auth/refresh').flush({}, {status:401,statusText:'Unauthorized'});
    expect(service.getToken()).toBeNull();expect(service.getRole()).toBeNull();
  });
});
