import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { GroupsComponent } from './groups';

describe('Group management', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({imports: [GroupsComponent], providers: [
      provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), provideRouter([])
    ]}).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function setup() {
    const fixture = TestBed.createComponent(GroupsComponent);
    fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/groups')).flush(['A', 'B']);
    http.expectOne(r => r.url.endsWith('/Students')).flush([{id:'student',fullName:'Test',login:'test',role:'Student',group:'A'}]);
    fixture.detectChanges();
    return {fixture, component: fixture.componentInstance};
  }
  it('keeps the entered name and reports a duplicate without adding a phantom group', () => {
    const {component} = setup(); component.name = 'A'; component.create();
    http.expectOne(r => r.method === 'POST').flush({message:'Duplicate group'}, {status:409,statusText:'Conflict'});
    expect(component.groups).toEqual(['A','B']); expect(component.name).toBe('A');
    expect(component.error).toBe('Duplicate group'); expect(component.saving).toBeFalse();
  });
  it('shows a new empty group immediately and selects it', () => {
    const {fixture,component} = setup(); component.name = ' C '; component.create();
    const request = http.expectOne(r => r.method === 'POST'); expect(request.request.body.name).toBe('C'); request.flush({name:'C'});
    fixture.detectChanges(); expect(component.selected).toBe('C'); expect(component.members).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('У цій групі ще немає студентів.');
  });
  it('does not move a student locally when the server refuses an active-attempt transfer', () => {
    const {component} = setup(); component.select('B'); component.studentId = 'student'; component.assign();
    http.expectOne(r => r.method === 'PUT').flush({message:'Active attempt'}, {status:409,statusText:'Conflict'});
    expect(component.students[0].group).toBe('A'); expect(component.error).toBe('Active attempt'); expect(component.assigning).toBeFalse();
  });
});
