import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActiveExamsHub } from './active-exams-hub';

describe('ActiveExamsHub', () => {
  let component: ActiveExamsHub;
  let fixture: ComponentFixture<ActiveExamsHub>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActiveExamsHub]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ActiveExamsHub);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
