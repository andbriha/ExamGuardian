import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { ReportsHubComponent } from './reports-hub';
import { By } from '@angular/platform-browser';
import { GroupResultsComponent } from '../group-results/group-results';
import { ExamSessionDto } from '../../../services/session.service';

describe('Teacher review workflow', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({imports: [ReportsHubComponent], providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), provideRouter([])]}).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function setup() {
    const fixture = TestBed.createComponent(ReportsHubComponent); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/grade-exports')).flush([]);
    http.expectOne(r => r.url.endsWith('/ExamSessions')).flush([]);
    http.expectOne(r => r.url.endsWith('/Exams')).flush([]);
    fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/Students')).flush([]);
    const component = fixture.componentInstance;
    component.openReview('1');
    http.expectOne(r => r.url.endsWith('/ExamSessions/1')).flush({id: 1, examId: 2, isFinished: true, evidenceStatus: 'Delivered', pendingObservationCount: 1, confirmedObservationCount: 0, integrityVerdict: 'UnderReview'});
    return {fixture, component};
  }
  it('blocks acceptance until all observations are dismissed and explains why', () => {
    const {fixture,component} = setup(); component.reviewNotes = 'Checked'; fixture.detectChanges();
    expect(component.reviewBlock('Accepted')).toContain('сигнали');
    component.reviewVerdict = 'Accepted'; component.saveReview(); http.expectNone(r => r.method === 'POST');
    component.review!.pendingObservationCount = 0; component.review!.evidenceStatus = 'Partial';
    expect(component.reviewBlock('Accepted')).toContain('доказів');
    expect(component.reviewBlock('Rejected')).toContain('підтвердьте');
    expect(component.reviewBlock('Inconclusive')).toBe('');
  });
  it('preserves the reason when the server rejects a stale decision', () => {
    const {component} = setup(); component.review!.pendingObservationCount = 0; component.reviewNotes = 'Recordings checked'; component.reviewVerdict = 'Accepted';
    component.saveReview(); component.saveReview();
    const request = http.expectOne(r => r.method === 'POST');
    expect(request.request.body).toEqual({verdict:'Accepted',notes:'Recordings checked'});
    request.flush({message:'Late evidence arrived'}, {status:409,statusText:'Conflict'});
    expect(component.reviewNotes).toBe('Recordings checked'); expect(component.reviewError).toBe('Late evidence arrived'); expect(component.reviewSaving).toBeFalse();
  });
  it('offers an actionable decision for DEV without allowing acceptance', () => {
    const {component} = setup();
    component.openReview('2');
    http.expectOne(r => r.url.endsWith('/ExamSessions/2')).flush({id:2, isFinished:true, identityVerificationSkipped:true, evidenceStatus:'Delivered', pendingObservationCount:0, confirmedObservationCount:0});
    expect(component.reviewVerdict).toBe('');
    expect(component.reviewBlock('Accepted')).toContain('DEV');
    expect(component.reviewBlock('Inconclusive')).toBe('');
  });
  it('keeps the group, exam and search while refreshing the archive', () => {
    const {fixture, component} = setup();
    const sheet = fixture.debugElement.query(By.directive(GroupResultsComponent)).componentInstance as GroupResultsComponent;
    sheet.group = 'A'; sheet.examId = 42; sheet.search = 'Anna';
    component.loadReports(); fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(GroupResultsComponent)).componentInstance).toBe(sheet);
    http.expectOne(r => r.url.endsWith('/ExamSessions')).flush([]);
    http.expectOne(r => r.url.endsWith('/Exams')).flush([]);
    fixture.detectChanges();
    expect(sheet.group).toBe('A'); expect(sheet.examId).toBe(42); expect(sheet.search).toBe('Anna');
  });
});
