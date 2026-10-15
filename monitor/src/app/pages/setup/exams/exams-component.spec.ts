import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExamsComponent } from './exams';

describe('ExamsComponent', () => {
  let component: ExamsComponent;
  let fixture: ComponentFixture<ExamsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
      imports: [ExamsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExamsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('transitions from scheduled to active to completed at the absolute boundaries', () => {
    spyOn(Date,'now').and.returnValue(Date.parse('2026-10-15T10:00:00Z'));
    component.exams=[{id:1,title:'Exam',description:'',startTime:'2026-10-15T10:00:00Z',durationMinutes:60,questionsCount:1,status:'upcoming'}];
    component.updateStatuses();expect(component.exams[0].status).toBe('active');
    (Date.now as jasmine.Spy).and.returnValue(Date.parse('2026-10-15T11:00:00Z'));
    component.updateStatuses();expect(component.exams[0].status).toBe('completed');
  });
  it('should create' , () => {
    expect(component).toBeTruthy();
  });
});
