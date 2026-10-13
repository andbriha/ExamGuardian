import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReportsHub } from './reports-hub';

describe('ReportsHub', () => {
  let component: ReportsHub;
  let fixture: ComponentFixture<ReportsHub>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportsHub]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReportsHub);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
