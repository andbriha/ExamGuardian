import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface QuestionOptionDto {
  id?: number;
  text: string;
  orderIndex: number;
}

export interface QuestionDto {
  id?: number;
  text: string;
  points: number;
  correctIndex?: number;
  orderIndex?: number;
  options: QuestionOptionDto[];
}

export interface ExamDto {
  id: number;
  title: string;
  description?: string;
  durationMinutes: number;
  scheduledStartTime?: string;
  createdAt?: string;
  creatorId?: string;
  questions?: QuestionDto[];
}

export interface CreateExamDto {
  title: string;
  description?: string;
  scheduledStartTime?: string;
  durationMinutes: number;
  creatorId?: string;
  questions: {
    text: string;
    points: number;
    correctIndex: number;
    orderIndex: number;
    options: string[];
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class ExamService {
  private apiUrl = `${environment.apiUrl}/Exams`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<ExamDto[]> {
    return this.http.get<ExamDto[]>(this.apiUrl);
  }

  getById(id: number): Observable<ExamDto> {
    return this.http.get<ExamDto>(`${this.apiUrl}/${id}`);
  }

  create(dto: CreateExamDto): Observable<ExamDto> {
    return this.http.post<ExamDto>(this.apiUrl, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
