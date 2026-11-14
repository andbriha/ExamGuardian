import { Component, Input, ChangeDetectorRef, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
interface GroupReport {group:string;exam:string;students:{name:string;status:string;grade:number|null;categories:{category:string;earned:number;maximum:number}[]}[];}
@Component({selector:'app-group-report-download',standalone:true,imports:[CommonModule],template:`
  <section class="report">
    <strong>Результати для студентів</strong>
    <p>PDF всієї групи за цим іспитом: підтверджені оцінки та бали за категоріями. Без паролів, відповідей і матеріалів спостереження.</p>
    <button (click)="prepare()" [disabled]="busy || !examId">{{busy ? 'Формування…' : 'Підготувати груповий PDF'}}</button>
    @if(error){<p role="alert">{{error}}</p>}
    @if(preview;as p){
      <h4>{{p.exam}} · {{p.group}}</h4>
      <p>У документі: {{p.students.length}} студентів. Підтверджених оцінок: {{approved}}. Решта матимуть статус без бала. Пошук і фільтр таблиці вище не скорочують PDF.</p>
      <div class="scroll"><table><thead><tr><th>Студент</th><th>Оцінка / 100</th><th>Бали за категоріями</th><th>Статус</th></tr></thead><tbody>
        @for(s of p.students;track $index){<tr><td>{{s.name}}</td><td>{{s.grade ?? '—'}}</td><td>@for(c of s.categories;track c.category){<div>{{c.category}}: {{c.earned}} / {{c.maximum}}</div>}</td><td>{{s.status}}</td></tr>}
      </tbody></table></div>
      <button (click)="download()" [disabled]="busy || !approved">Завантажити результати PDF</button>
      @if(!approved){<p>Ще немає підтверджених оцінок. Спочатку перевірте та прийміть результати спроб.</p>}
    }
  </section>`,styles:[`.report{padding:18px;border:1px solid #b8d4da;background:#f0f9fb;border-radius:10px;margin:18px 0}p{line-height:1.5}button{padding:10px 16px;border:1px solid #0f766e;border-radius:8px;background:white;color:#115e59;cursor:pointer}button:disabled{opacity:.5}.scroll{overflow:auto;margin:14px 0}table{width:100%;border-collapse:collapse}td,th{padding:10px;text-align:left;border-bottom:1px solid #cbd5e1}[role=alert]{color:#9f1239}`]})
export class GroupReportDownloadComponent implements OnChanges {
  @Input() group=''; @Input() examId=0;
  busy=false;error='';preview:GroupReport|null=null;private epoch=0;
  constructor(private http:HttpClient, private cdr:ChangeDetectorRef){}
  ngOnChanges(){this.epoch++;this.preview=null;this.error='';this.busy=false;}
  get approved(){return this.preview?.students.filter(s=>s.grade!==null).length||0;}
  prepare(){
    if(this.busy||!this.examId)return;this.busy=true;this.error='';const epoch=this.epoch;
    this.http.get<GroupReport>(`${environment.apiUrl}/group-results`,{params:{group:this.group,examId:this.examId}}).subscribe({next:p=>{if(epoch!==this.epoch)return;this.preview=p;this.busy=false;this.cdr.markForCheck();},error:e=>{if(epoch!==this.epoch)return;this.error=e.error?.message||'Не вдалося отримати результати.';this.busy=false;this.cdr.markForCheck();}});
  }
  download(){
    if(this.busy||!this.approved)return;this.busy=true;this.error='';const epoch=this.epoch;const examId=this.examId;
    this.http.get(`${environment.apiUrl}/group-results`,{params:{group:this.group,examId,format:'pdf'},responseType:'blob'}).subscribe({next:blob=>{
      if(epoch!==this.epoch)return;const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`group-results-${examId}.pdf`;a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);this.busy=false;this.cdr.markForCheck();
    },error:async e=>{if(epoch!==this.epoch)return;let message='Не вдалося сформувати PDF. Спробуйте ще раз.';try{message=JSON.parse(await e.error.text()).message||message;}catch{}if(epoch!==this.epoch)return;this.error=message;this.busy=false;this.cdr.markForCheck();}});
  }
}
