import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ExamSessionDto {
  id: number;
  studentId: string;
  studentName: string;
  examId: number;
  examTitle: string;
  startTime: string;
  endTime?: string;
  score?: number;
  isFinished: boolean;
  violationCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class SessionService {
  private apiUrl = `${environment.apiUrl}/ExamSessions`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<ExamSessionDto[]> {
    return this.http.get<ExamSessionDto[]>(this.apiUrl);
  }

  getById(id: number): Observable<ExamSessionDto> {
    return this.http.get<ExamSessionDto>(`${this.apiUrl}/${id}`);
  }

  getByStudent(studentId: string): Observable<ExamSessionDto[]> {
    return this.http.get<ExamSessionDto[]>(`${this.apiUrl}/by-student/${studentId}`);
  }

  getByExam(examId: number): Observable<ExamSessionDto[]> {
    return this.http.get<ExamSessionDto[]>(`${this.apiUrl}/by-exam/${examId}`);
  }
}
