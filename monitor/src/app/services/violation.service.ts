import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ViolationAlertDto {
  id: number;
  examSessionId: number;
  violationType: string;
  confidence: number;
  relativeTimestampSeconds: number;
  evidenceUrl?: string;
  metadata?: string;
  timestamp: string;
  isReviewed: boolean;
}

export interface VideoChunkDto {
  chunkIndex: number;
  fileName: string;
  sizeBytes: number;
  url: string;
}

@Injectable({
  providedIn: 'root'
})
export class ViolationService {
  private apiUrl = `${environment.apiUrl}/Violations`;

  constructor(private http: HttpClient) {}

  getUnreviewed(): Observable<ViolationAlertDto[]> {
    return this.http.get<ViolationAlertDto[]>(`${this.apiUrl}/unreviewed`);
  }

  getById(id: number): Observable<ViolationAlertDto> {
    return this.http.get<ViolationAlertDto>(`${this.apiUrl}/${id}`);
  }

  getBySession(sessionId: number): Observable<ViolationAlertDto[]> {
    return this.http.get<ViolationAlertDto[]>(`${this.apiUrl}/by-session/${sessionId}`);
  }

  markAsReviewed(id: number): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/review`, {});
  }

  getVideoChunks(sessionId: string | number): Observable<VideoChunkDto[]> {
    return this.http.get<VideoChunkDto[]>(`${environment.apiUrl}/video/${sessionId}/chunks`);
  }
}
