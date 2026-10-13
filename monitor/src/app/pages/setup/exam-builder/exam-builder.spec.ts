import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExamBuilder } from './exam-builder';

describe('ExamBuilder', () => {
  let component: ExamBuilder;
  let fixture: ComponentFixture<ExamBuilder>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExamBuilder]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExamBuilder);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
