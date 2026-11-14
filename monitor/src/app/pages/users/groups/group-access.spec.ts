import {provideZonelessChangeDetection} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {GroupAccessComponent} from './group-access';

describe('Group access management', () => {
  let component: GroupAccessComponent;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({imports: [GroupAccessComponent], providers: [
      provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()
    ]});
    component = TestBed.createComponent(GroupAccessComponent).componentInstance;
    http = TestBed.inject(HttpTestingController);
    component.group = 'A';
  });
  afterEach(() => http.verify());
  it('does not show permissions from a previously selected group', () => {
    component.load();
    const pending = http.expectOne('/api/groups/A/access');
    component.group = 'B';
    component.ngOnChanges();
    pending.flush([{id: 'teacher', fullName: 'Teacher', administrator: false, assigned: true}]);
    expect(component.opened).toBeFalse();
    expect(component.teachers).toEqual([]);
  });
  it('does not pretend a denied assignment succeeded', () => {
    const teacher = {id: 'teacher', fullName: 'Teacher', administrator: false, assigned: false};
    component.change(teacher);
    http.expectOne('/api/groups/A/examiners/teacher').flush({}, {status: 403, statusText: 'Forbidden'});
    expect(teacher.assigned).toBeFalse();
    expect(component.error).toBeTruthy();
    expect(component.busy).toBeFalse();
  });
});
