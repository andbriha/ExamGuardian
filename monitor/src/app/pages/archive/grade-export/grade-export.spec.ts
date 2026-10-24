import { provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GradeExportComponent } from './grade-export';

describe('Group-based grade export',()=>{
  let http:HttpTestingController;
  beforeEach(async()=>{await TestBed.configureTestingModule({imports:[GradeExportComponent],providers:[provideZonelessChangeDetection(),provideHttpClient(),provideHttpClientTesting()]}).compileComponents();http=TestBed.inject(HttpTestingController);});
  afterEach(()=>http.verify());
  function setup(){const fixture=TestBed.createComponent(GradeExportComponent);fixture.detectChanges();http.expectOne(r=>r.url.endsWith('/grade-exports')).flush([
    {group:'A',exams:[{examId:7,title:'Math',date:'2026-10-24T09:00:00Z',eligibleCount:2,excludedCount:1}],exclusions:[{code:'unapproved',message:'Не перевірено',count:1}]},
    {group:'B',exams:[],exclusions:[{code:'unfinished',message:'Спроба ще не завершена',count:1}]}]);return {fixture,component:fixture.componentInstance};}
  it('requires choosing a group and explains an empty group without offering downloads',()=>{
    const {fixture,component}=setup();fixture.detectChanges();expect(fixture.nativeElement.textContent).not.toContain('Math');
    component.selectedGroup='B';fixture.changeDetectorRef.markForCheck();fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Спроба ще не завершена');expect(fixture.nativeElement.textContent).not.toContain('Завантажити CSV');
    component.selectedGroup='A';fixture.changeDetectorRef.markForCheck();fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Math');expect(fixture.nativeElement.textContent).toContain('2 оцінок готові');
  });
  it('downloads a real CSV blob with the selected group and prevents duplicate requests',()=>{
    const {component}=setup();component.selectedGroup='A';
    spyOn(URL,'createObjectURL').and.returnValue('blob:test');spyOn(HTMLAnchorElement.prototype,'click');
    component.download(component.selected!.exams[0]);component.download(component.selected!.exams[0]);
    const request=http.expectOne(r=>r.url.endsWith('/exams/7/grades'));
    expect(request.request.params.get('group')).toBe('A');expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['username,grade\r\nstudent,100\r\n'],{type:'text/csv'}));
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();expect(component.success).toContain('CSV');expect(component.downloading).toBeNull();
  });
});
