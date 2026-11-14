import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActiveExamsHubComponent } from './active-exams-hub';

describe('ActiveExamsHubComponent', () => {
  let component: ActiveExamsHubComponent;
  let fixture: ComponentFixture<ActiveExamsHubComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
      imports: [ActiveExamsHubComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ActiveExamsHubComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
