import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
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
  scoreCalculated?: boolean;
  passingScoreAtStart: number;
  isFinished: boolean;
  violationCount: number;
  pendingObservationCount?: number;
  confirmedObservationCount?: number;
  deadlineUtc?: string;
  lastSeenAt?: string;
  lastAnalysisAt?: string;
  lastAudioAt?: string;
  status: string;
  evidenceStatus: string;
  integrityVerdict: string;
  integrityNotes?: string;
  integrityReviewedAt?: string;
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
  getPreview(id: number): Observable<HttpResponse<Blob>> { return this.http.get(`${this.apiUrl}/${id}/preview`, { responseType: 'blob', observe: 'response' }); }
  terminate(id: number): Observable<ExamSessionDto> { return this.http.post<ExamSessionDto>(`${this.apiUrl}/${id}/terminate`, {}); }
  review(id: number, verdict: string, notes: string): Observable<ExamSessionDto> { return this.http.post<ExamSessionDto>(`${this.apiUrl}/${id}/review`,{verdict,notes}); }
}
