import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GradeExportService, ExportGroup, ExportExam } from '../../../services/grade-export.service';
@Component({selector:'app-grade-export',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./grade-export.html',styleUrl:'./grade-export.css'})
export class GradeExportComponent implements OnInit {
  groups:ExportGroup[]=[];
  selectedGroup:string|null=null;
  loading=false;
  downloading:number|null=null;
  error='';
  success='';
  constructor(private service:GradeExportService,private cdr:ChangeDetectorRef) {}
  ngOnInit() { this.refresh(); }
  get selected() { return this.groups.find(g => g.group === this.selectedGroup); }
  refresh(clearError=true) {
    this.loading=true; if(clearError) this.error='';
    this.service.catalog().subscribe({next:groups=>{
      this.groups=groups;
      if(!groups.some(g=>g.group===this.selectedGroup)) this.selectedGroup=null;
      this.loading=false; this.cdr.markForCheck();
    },error:()=>{this.loading=false;this.error='Не вдалося перевірити доступність оцінок. Повторіть оновлення.';this.groups=[];this.cdr.markForCheck();}});
  }
  selectGroup() { this.error=''; this.success=''; }
  download(exam:ExportExam) {
    if(this.downloading!==null || this.selectedGroup===null) return;
    this.downloading=exam.examId;this.error='';this.success='';
    this.service.download(exam.examId,this.selectedGroup).subscribe({next:blob=>{
      const url=URL.createObjectURL(blob);const link=document.createElement('a');
      link.href=url;link.download=`exam-${exam.examId}-grades.csv`;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
      this.success='CSV завантажено. Перевірте зіставлення логінів і колонок у Moodle.';this.downloading=null;this.cdr.markForCheck();
    },error:async error=>{
      try { this.error=JSON.parse(await error.error.text()).message; } catch { this.error='Не вдалося завантажити CSV. Спробуйте ще раз.'; }
      this.downloading=null;this.refresh(false);this.cdr.markForCheck();
    }});
  }
}
