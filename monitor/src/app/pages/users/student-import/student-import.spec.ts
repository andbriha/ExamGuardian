import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { StudentImportComponent } from './student-import';

describe('Roster preview workflow', () => {
  beforeEach(() => TestBed.configureTestingModule({imports:[StudentImportComponent],providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting(),provideRouter([])]}));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function setup() {
    const c=TestBed.createComponent(StudentImportComponent).componentInstance;
    TestBed.inject(HttpTestingController).expectOne(r=>r.url.endsWith('/groups')).flush(['A']);
    c.group='A';c.text='Student One';return c;
  }
  it('never commits a preview with row errors and invalidates it after edits',()=>{
    const c=setup(),h=TestBed.inject(HttpTestingController);c.check();
    h.expectOne(r=>r.url.endsWith('/preview')).flush({group:'A',token:null,rows:[{row:1,fullName:'Student One',login:'',action:'create',error:'Duplicate'}]});
    c.commit();h.expectNone(r=>r.url.endsWith('/commit'));
    c.invalidate();expect(c.preview).toBeNull();
  });
  it('retries the same protected plan after a lost response',()=>{
    const c=setup(),h=TestBed.inject(HttpTestingController);c.check();
    h.expectOne(r=>r.url.endsWith('/preview')).flush({group:'A',token:'plan',rows:[]});
    c.commit();h.expectOne(r=>r.url.endsWith('/commit')).error(new ProgressEvent('network'));
    expect(c.preview?.token).toBe('plan');c.commit();
    const retry=h.expectOne(r=>r.url.endsWith('/commit'));expect(retry.request.body).toEqual({token:'plan'});
    retry.flush({group:'A',created:0,skipped:1});h.expectOne(r=>r.url.endsWith('/Students')).flush([]);
    expect(c.result?.skipped).toBe(1);
  });
});
