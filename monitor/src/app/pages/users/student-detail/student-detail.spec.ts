import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { StudentDetailComponent } from './student-detail';

describe('Student detail', () => {
  it('loads its actual route id and requests the protected photo', async () => {
    TestBed.configureTestingModule({providers: [provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting(),provideRouter([{path:'student/:id',component:StudentDetailComponent}])]});
    const harness = await RouterTestingHarness.create('/student/test-student');
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/Students/test-student').flush({id:'test-student',fullName:'Test Participant',login:'participant',group:'Test',role:'Student'});
    http.expectOne('/api/Students/test-student/photo').flush(new Blob(['test'],{type:'image/jpeg'}));
    harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Test Participant');
    http.verify();
  });
});
