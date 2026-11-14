import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { GroupService } from '../../../services/group.service';
import { StudentService, StudentDto } from '../../../services/student.service';
import { GroupCredentialsComponent } from '../group-credentials/group-credentials';

interface Preview { group: string; token: string | null; rows: {row:number; fullName:string; login:string; action:string; error:string|null}[]; }
@Component({selector:'app-student-import', standalone:true, imports:[CommonModule,FormsModule,RouterLink,GroupCredentialsComponent], templateUrl:'./student-import.html', styleUrl:'./student-import.css'})
export class StudentImportComponent {
  group = ''; text = ''; groups: string[] = []; busy = false; error = ''; preview: Preview | null = null;
  result: {group:string; created:number; skipped:number} | null = null; members: StudentDto[] = [];
  constructor(private http: HttpClient, private service: StudentService, groups: GroupService, route: ActivatedRoute, private cdr: ChangeDetectorRef) {
    this.group = route.snapshot.queryParamMap.get('group') || '';
    groups.getAll().subscribe({next: value => {this.groups=value;this.cdr.markForCheck();}, error: () => {}});
  }
  invalidate() { this.preview = null; this.error=''; this.result=null; this.members=[]; }
  get creates() { return this.preview?.rows.filter(r => r.action === 'create' && !r.error).length || 0; }
  get skips() { return this.preview?.rows.filter(r => r.action === 'skip' && !r.error).length || 0; }
  async file(event: Event) {
    const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value=''; if (!file) return;
    this.invalidate();
    if (file.size > 200000 || !/\.(csv|tsv|txt)$/i.test(file.name)) {this.error='Оберіть CSV UTF-8, TSV або TXT до 200 КБ. Для Excel можна просто скопіювати комірки.';return;}
    try { this.text = new TextDecoder('utf-8', {fatal:true}).decode(await file.arrayBuffer()); }
    catch { this.error='Збережіть файл у кодуванні UTF-8.'; }
    this.cdr.markForCheck();
  }
  template() {
    const url=URL.createObjectURL(new Blob(['\uFEFFfull_name,username\r\nМарія Коваль,\r\nAlex Smith,alex.smith\r\n'],{type:'text/csv;charset=utf-8'}));
    const a=document.createElement('a');a.href=url;a.download='students-template.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  check() {
    if(this.busy) return; this.busy=true;this.error='';this.preview=null;this.result=null;
    this.http.post<Preview>(`${environment.apiUrl}/students/import/preview`,{group:this.group,text:this.text}).subscribe({next:p=>{this.preview=p;this.busy=false;this.cdr.markForCheck();},error:e=>{this.error=e.error?.message||'Не вдалося перевірити список.';this.busy=false;this.cdr.markForCheck();}});
  }
  commit() {
    if(this.busy || !this.preview?.token) return;this.busy=true;this.error='';
    this.http.post<{group:string;created:number;skipped:number}>(`${environment.apiUrl}/students/import/commit`,{token:this.preview.token}).subscribe({next:r=>{
      this.result=r;this.preview=null;this.busy=false;this.cdr.markForCheck();
      this.service.getAll().subscribe({next:members=>{this.members=members.filter(s=>s.group===r.group);this.cdr.markForCheck();},error:()=>{this.error='Студентів додано. Доступи можна завантажити на сторінці групи.';this.cdr.markForCheck();}});
    },error:e=>{this.error=e.error?.message||'Не отримано підтвердження. Повторіть цей запит: уже створені акаунти не дублюватимуться.';this.busy=false;this.cdr.markForCheck();}});
  }
}
