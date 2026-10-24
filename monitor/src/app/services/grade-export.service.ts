import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
export interface ExportExam { examId:number; title:string; date:string; eligibleCount:number; excludedCount:number; }
export interface ExportGroup { group:string; exams:ExportExam[]; exclusions:{code:string;message:string;count:number}[]; }
@Injectable({providedIn:'root'})
export class GradeExportService {
  constructor(private http:HttpClient) {}
  catalog() { return this.http.get<ExportGroup[]>(`${environment.apiUrl}/grade-exports`); }
  download(examId:number, group:string) {
    return this.http.get(`${environment.apiUrl}/exams/${examId}/grades`, {params:{group},responseType:'blob'});
  }
}
