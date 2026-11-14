import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ViolationAlertDto {
  id: number;
  examSessionId: number;
  violationType: string;
  priority?: 'Observation' | 'Warning' | 'Intervention';
  confidence: number;
  relativeTimestampSeconds: number;
  evidenceUrl?: string;
  metadata?: string;
  timestamp: string;
  eventId: string;
  isReviewed: boolean;
  reviewDecision: "Pending" | "Confirmed" | "Dismissed" | "NoViolation" | "InsufficientEvidence" | "LegacyReviewed";
  reviewVersion?: string;
  reviewNotes?: string;
  reviewHistoryJson?: string;
  reviewedAt?: string;
}

export interface VideoChunkDto {
  chunkIndex: number;
  fileName: string;
  sizeBytes: number;
  url: string;
  metadata?: { startOffsetSeconds?: number; durationSeconds?: number; stream: string; sha256?: string };
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

  markAsReviewed(id: number, decision: "Confirmed" | "Dismissed" | "NoViolation" | "InsufficientEvidence", notes: string, expectedVersion?: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/review`, { decision, notes, expectedVersion });
  }

  getVideoChunks(sessionId: string | number, stream: "camera" | "screen" = "camera"): Observable<VideoChunkDto[]> {
    return this.http.get<VideoChunkDto[]>(`${environment.apiUrl}/video/${sessionId}/chunks?stream=${stream}`);
  }
  getEvidence(eventId: string): Observable<Blob> { return this.http.get(`${this.apiUrl}/evidence/${eventId}`, {responseType: 'blob'}); }
}
