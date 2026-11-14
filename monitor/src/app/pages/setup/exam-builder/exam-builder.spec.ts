import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExamBuilderComponent } from './exam-builder';

describe('ExamBuilderComponent', () => {
  let component: ExamBuilderComponent;
  let fixture: ComponentFixture<ExamBuilderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
      imports: [ExamBuilderComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExamBuilderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
