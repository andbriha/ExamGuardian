import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GroupReportDownloadComponent } from './group-report-download';

describe('Group report preview',()=>{
  beforeEach(()=>TestBed.configureTestingModule({imports:[GroupReportDownloadComponent],providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]}));
  afterEach(()=>TestBed.inject(HttpTestingController).verify());
  it('ignores a response from a previously selected group',()=>{
    const c=TestBed.createComponent(GroupReportDownloadComponent).componentInstance,h=TestBed.inject(HttpTestingController);
    c.group='A';c.examId=12;c.prepare();const request=h.expectOne(r=>r.params.get('group')==='A'&&r.params.get('examId')==='12');
    c.group='B';c.ngOnChanges();request.flush({group:'A',exam:'Old exam',students:[]});expect(c.preview).toBeNull();expect(c.busy).toBeFalse();
  });
  it('does not offer a file containing no published marks',()=>{
    const fixture=TestBed.createComponent(GroupReportDownloadComponent),c=fixture.componentInstance,h=TestBed.inject(HttpTestingController);
    c.group='A';c.examId=12;c.prepare();h.expectOne(r=>r.url.endsWith('/group-results')).flush({group:'A',exam:'Exam',students:[{name:'One',status:'На перевірці',grade:null,categories:[]}]});
    c.download();h.expectNone(r=>r.params.get('format')==='pdf');expect(c.approved).toBe(0);
  });
});
