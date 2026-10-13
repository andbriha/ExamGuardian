import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface StudentDto {
  id: string;
  fullName: string;
  login: string;
  role: string;
  group?: string;
  photoUrl?: string;
  isActive?: boolean;
}

export interface CreateStudentDto {
  fullName: string;
  login: string;
  password?: string;
  role?: string;
  group?: string;
}

@Injectable({
  providedIn: 'root'
})
export class StudentService {
  private apiUrl = `${environment.apiUrl}/Students`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<StudentDto[]> {
    return this.http.get<StudentDto[]>(this.apiUrl);
  }

  getById(id: string): Observable<StudentDto> {
    return this.http.get<StudentDto>(`${this.apiUrl}/${id}`);
  }

  create(dto: CreateStudentDto): Observable<StudentDto> {
    return this.http.post<StudentDto>(this.apiUrl, dto);
  }

  uploadPhoto(id: string, file: File): Observable<{ photoUrl: string }> {
    const formData = new FormData();
    formData.append('photo', file);
    return this.http.post<{ photoUrl: string }>(`${this.apiUrl}/${id}/photo`, formData);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getPhotoUrl(id: string): string {
    return `${this.apiUrl}/${id}/photo`;
  }
}
