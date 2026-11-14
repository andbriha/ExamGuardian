import {Component,Input,OnChanges,ChangeDetectorRef} from '@angular/core';
import {CommonModule} from '@angular/common';
import {HttpClient} from '@angular/common/http';
import {environment} from '../../../../environments/environment';
interface TeacherAccess {id:string;fullName:string;administrator:boolean;assigned:boolean;}
@Component({selector:'app-group-access',standalone:true,imports:[CommonModule],template:`
  <button type="button" (click)="load()" [disabled]="busy || !group">Доступ викладачів до групи</button>
  <p *ngIf="error" role="alert">{{error}}</p>
  <section *ngIf="opened" aria-label="Доступ до групи">
    <p>Призначення дозволяє працювати зі студентами, фото та обліковими даними цієї групи.</p>
    <ul><li *ngFor="let teacher of teachers">{{teacher.fullName}}
      <span *ngIf="teacher.administrator"> · Адміністратор</span>
      <button *ngIf="!teacher.administrator" type="button" [disabled]="busy" (click)="change(teacher)">{{teacher.assigned ? 'Відкликати доступ' : 'Надати доступ'}}</button>
    </li></ul>
  </section>`})
export class GroupAccessComponent implements OnChanges {
  @Input() group=''; teachers:TeacherAccess[]=[];opened=false;busy=false;error='';private epoch=0;
  constructor(private http:HttpClient,private cdr:ChangeDetectorRef){}
  ngOnChanges(){++this.epoch;this.opened=false;this.busy=false;this.error='';this.teachers=[];}
  private url(){return `${environment.apiUrl}/groups/${encodeURIComponent(this.group)}`;}
  load(){if(this.busy || !this.group)return;const epoch=this.epoch;this.busy=true;this.error='';
    this.http.get<TeacherAccess[]>(this.url()+'/access').subscribe({next:rows=>{if(epoch!==this.epoch)return;this.teachers=rows;this.opened=true;this.busy=false;this.cdr.markForCheck();},error:e=>{if(epoch!==this.epoch)return;this.busy=false;this.error=e.status===403?'Призначеннями керує адміністратор школи.':'Не вдалося завантажити доступ. Спробуйте ще раз.';this.cdr.markForCheck();}});
  }
  change(teacher:TeacherAccess){if(this.busy)return;const epoch=this.epoch;this.busy=true;this.error='';const url=this.url()+'/examiners/'+teacher.id;
    (teacher.assigned?this.http.delete(url):this.http.put(url,{})).subscribe({next:()=>{if(epoch!==this.epoch)return;teacher.assigned=!teacher.assigned;this.busy=false;this.cdr.markForCheck();},error:()=>{if(epoch!==this.epoch)return;this.busy=false;this.error='Доступ не змінено. Оновіть список і спробуйте ще раз.';this.cdr.markForCheck();}});
  }
}
