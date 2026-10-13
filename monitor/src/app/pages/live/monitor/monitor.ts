import { Component, OnInit, OnDestroy, ViewChild, ElementRef, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';

import { ExamService, ExamDto } from '../../../services/exam.service';
import { StudentService, StudentDto } from '../../../services/student.service';
import { ViolationService, ViolationAlertDto, VideoChunkDto } from '../../../services/violation.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';

export interface StudentSessionView {
  sessionId: number;
  studentId: string;
  name: string;
  progress: string;
  status: 'ok' | 'warn' | 'offline';
  violationCount: number;
  score?: number;
}

export interface AlertView {
  id: string;
  sessionId: number;
  time: string;
  type: string;
  studentName: string;
  conf: number;
  relativeSec: number;
  raw: ViolationAlertDto;
}

@Component({
  selector: 'app-monitor',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './monitor.html',
  styleUrl: './monitor.css'
})
export class MonitorComponent implements OnInit, OnDestroy {
  @ViewChild('dispatcherVideo') videoRef?: ElementRef<HTMLVideoElement>;
  readonly Math = Math;

  examId: string | null = null;
  exam: ExamDto | null = null;
  
  students: StudentSessionView[] = [];
  selectedSession: StudentSessionView | null = null;
  alerts: AlertView[] = [];
  
  // Video & Timeline states
  sessionViolations: ViolationAlertDto[] = [];
  videoChunks: VideoChunkDto[] = [];
  selectedChunkIndex: number = 1;
  currentVideoUrl: string = '';
  
  currentTime: number = 0;
  duration: number = 60;
  isPlaying: boolean = false;
  playbackRate: number = 1.0;
  hasVideo: boolean = false;
  isLoadingVideo: boolean = false;
  activeViolationMarker: ViolationAlertDto | null = null;
  
  isLoading = true;
  private pollInterval: any = null;

  constructor(
    private route: ActivatedRoute,
    private examService: ExamService,
    private studentService: StudentService,
    private violationService: ViolationService,
    private sessionService: SessionService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.examId = this.route.snapshot.paramMap.get('id');
    this.loadData();
    this.pollInterval = setInterval(() => this.refreshAlerts(), 8000);
  }

  ngOnDestroy() {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
    }
  }

  loadData() {
    this.isLoading = true;
    const numId = this.examId ? parseInt(this.examId, 10) : NaN;
    if (!isNaN(numId)) {
      this.examService.getById(numId).subscribe({
        next: (ex) => {
          this.exam = ex;
          this.loadSessions(numId);
        },
        error: () => {
          this.loadSessions(numId);
        }
      });
    } else {
      this.loadSessions(0);
    }
  }

  private loadSessions(examIdNum: number) {
    this.sessionService.getAll().subscribe({
      next: (sessions) => {
        const relevant = examIdNum > 0 ? sessions.filter(s => s.examId === examIdNum) : sessions;
        if (relevant && relevant.length > 0) {
          this.students = relevant.map((s, idx) => ({
            sessionId: s.id,
            studentId: s.studentId,
            name: s.studentName || `Студент #${s.studentId.substring(0, 8)}`,
            progress: s.isFinished ? 'Завершено' : 'Складання',
            status: s.violationCount > 0 ? 'warn' : 'ok',
            violationCount: s.violationCount,
            score: s.score
          }));
          // Auto select first session
          if (!this.selectedSession && this.students.length > 0) {
            this.selectSession(this.students[0]);
          }
        } else {
          this.fallbackLoadStudents();
        }
        this.refreshAlerts();
      },
      error: () => {
        this.fallbackLoadStudents();
        this.refreshAlerts();
      }
    });
  }

  private fallbackLoadStudents() {
    this.studentService.getAll().subscribe({
      next: (studentDtos) => {
        if (studentDtos && studentDtos.length > 0) {
          this.students = studentDtos.map((s, idx) => ({
            sessionId: idx + 1,
            studentId: s.id,
            name: s.fullName,
            progress: 'Складання',
            status: idx === 0 ? 'warn' : 'ok',
            violationCount: idx === 0 ? 3 : 0
          }));
          if (!this.selectedSession && this.students.length > 0) {
            this.selectSession(this.students[0]);
          }
        } else {
          this.students = [];
        }
        this.refreshAlerts();
      },
      error: () => {
        this.students = [];
        this.refreshAlerts();
      }
    });
  }

  selectSession(st: StudentSessionView) {
    this.selectedSession = st;
    this.isLoadingVideo = true;
    this.hasVideo = false;
    this.currentVideoUrl = '';
    this.sessionViolations = [];
    this.activeViolationMarker = null;

    // Load video chunks for this session
    this.violationService.getVideoChunks(st.sessionId).subscribe({
      next: (chunks) => {
        this.isLoadingVideo = false;
        if (chunks && chunks.length > 0) {
          this.videoChunks = chunks;
          this.hasVideo = true;
          this.selectedChunkIndex = chunks[0].chunkIndex;
          this.currentVideoUrl = chunks[0].url;
        } else {
          this.videoChunks = [];
          this.hasVideo = false;
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoadingVideo = false;
        this.hasVideo = false;
        this.cdr.detectChanges();
      }
    });

    // Load violations for this session
    this.violationService.getBySession(st.sessionId).subscribe({
      next: (vios) => {
        this.sessionViolations = vios || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.sessionViolations = [];
        this.cdr.detectChanges();
      }
    });
  }

  selectChunk(chunkIndex: number) {
    const chunk = this.videoChunks.find(c => c.chunkIndex === chunkIndex);
    if (!chunk) return;
    this.selectedChunkIndex = chunkIndex;
    this.currentVideoUrl = chunk.url;
    this.currentTime = 0;
    this.activeViolationMarker = null;
    this.cdr.detectChanges();
  }

  refreshAlerts() {
    this.violationService.getUnreviewed().subscribe({
      next: (unreviewed) => {
        this.isLoading = false;
        if (unreviewed && unreviewed.length > 0) {
          this.alerts = unreviewed.map(u => ({
            id: u.id.toString(),
            sessionId: u.examSessionId,
            time: new Date(u.timestamp).toLocaleTimeString(),
            type: this.getViolationLabel(u.violationType),
            studentName: `Сесія #${u.examSessionId}`,
            conf: Math.round(u.confidence * 100),
            relativeSec: u.relativeTimestampSeconds,
            raw: u
          }));
        } else {
          this.alerts = [];
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onMetadataLoaded(): void {
    if (this.videoRef?.nativeElement) {
      this.duration = this.videoRef.nativeElement.duration || 60;
      this.videoRef.nativeElement.playbackRate = this.playbackRate;
      this.cdr.detectChanges();
    }
  }

  onTimeUpdate(): void {
    if (this.videoRef?.nativeElement) {
      this.currentTime = this.videoRef.nativeElement.currentTime;
      this.cdr.detectChanges();
    }
  }

  togglePlay(): void {
    if (!this.videoRef?.nativeElement) return;
    if (this.videoRef.nativeElement.paused) {
      this.videoRef.nativeElement.play();
      this.isPlaying = true;
    } else {
      this.videoRef.nativeElement.pause();
      this.isPlaying = false;
    }
  }

  setSpeed(rate: number): void {
    this.playbackRate = rate;
    if (this.videoRef?.nativeElement) {
      this.videoRef.nativeElement.playbackRate = rate;
    }
  }

  seekToSeconds(sec: number): void {
    if (this.videoRef?.nativeElement) {
      this.videoRef.nativeElement.currentTime = Math.max(0, Math.min(sec, this.duration));
      this.currentTime = this.videoRef.nativeElement.currentTime;
      this.videoRef.nativeElement.play().catch(() => {});
      this.isPlaying = true;
    }
  }

  onTimelineClick(event: MouseEvent): void {
    const target = event.currentTarget as HTMLElement;
    const rect = target.getBoundingClientRect();
    const clickX = event.clientX - rect.left;
    const ratio = Math.max(0, Math.min(1, clickX / rect.width));
    const targetSec = ratio * this.duration;
    this.seekToSeconds(targetSec);
  }

  seekToViolation(v: ViolationAlertDto): void {
    this.activeViolationMarker = v;
    const targetChunk = Math.floor(v.relativeTimestampSeconds / 60) + 1;
    const offset = v.relativeTimestampSeconds % 60;

    if (targetChunk !== this.selectedChunkIndex && this.videoChunks.some(c => c.chunkIndex === targetChunk)) {
      this.selectChunk(targetChunk);
      setTimeout(() => {
        this.seekToSeconds(offset);
      }, 300);
    } else {
      this.seekToSeconds(offset);
    }
  }

  onAlertClicked(alert: AlertView): void {
    // If the alert is for another session, switch session first
    if (!this.selectedSession || this.selectedSession.sessionId !== alert.sessionId) {
      const match = this.students.find(s => s.sessionId === alert.sessionId);
      if (match) {
        this.selectSession(match);
        setTimeout(() => {
          this.seekToViolation(alert.raw);
        }, 500);
        return;
      }
    }
    this.seekToViolation(alert.raw);
  }

  getViolationsInCurrentChunk(): ViolationAlertDto[] {
    const startSec = (this.selectedChunkIndex - 1) * 60;
    const endSec = this.selectedChunkIndex * 60;
    return this.sessionViolations.filter(v => v.relativeTimestampSeconds >= startSec && v.relativeTimestampSeconds < endSec);
  }

  getMarkerPositionPercent(v: ViolationAlertDto): number {
    const offset = v.relativeTimestampSeconds % 60;
    const dur = this.duration > 0 ? this.duration : 60;
    return Math.max(0, Math.min(100, (offset / dur) * 100));
  }

  formatTime(sec: number): string {
    const total = Math.floor(sec || 0);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  getViolationLabel(type: string): string {
    const map: Record<string, string> = {
      'cell_phone': '📱 Телефон у руках / на столі',
      'micro_earpiece': '🎧 Мікронавушник у вусі',
      'earphone_detected': '🎧 Навушники виявлено',
      'looking_away_left': '👀 Погляд ліворуч (підказки зліва)',
      'looking_away_right': '👀 Погляд праворуч (підказки справа)',
      'looking_down': '⬇️ Погляд униз (шпаргалка / телефон)',
      'looking_away': '👀 Відведення погляду',
      'multiple_faces': '👥 Стороння особа в кадрі',
      'face_missing': '🚫 Відсутнє обличчя перед камерою',
      'voice_detected': '🎙️ Сторонній голос',
      'voice_activity': '🎙️ Розмова / голосові підказки',
      'background_noise': '🔊 Сторонній шум',
      'app_switched': '💻 Перемикання вікна',
      'prohibited_app': '🛑 Заборонена програма (Discord/Telegram тощо)',
      'discord_detected': '🛑 Discord у фоні',
      'telegram_detected': '🛑 Telegram у фоні',
      'remote_control': '⚠️ Віддалений доступ (AnyDesk/TeamViewer)',
      'virtual_machine': '🖥️ Запуск у віртуальній машині (VM)',
      'vm_detected': '🖥️ Віртуальна машина (VM)',
      'spoofed_process': '⚙️ Спроба маскування процесу (Spoofing)',
      'virtual_camera': '📹 Віртуальна камера (OBS/ManyCam)',
      'multiple_monitors': '🖥️ Підключено кілька моніторів'
    };
    return map[type] || `⚠️ ${type}`;
  }

  getViolationDetail(v: ViolationAlertDto | null): string {
    if (!v?.metadata) return '';
    try {
      const meta = JSON.parse(v.metadata);
      if (meta.desc) return meta.desc;
      if (meta.app) return `Програма: ${meta.app}`;
      if (meta.details) return `Деталі: ${meta.details}`;
      if (meta.direction && meta.yaw !== undefined) return `Поворот: ${meta.direction} (yaw: ${meta.yaw}, pitch: ${meta.pitch})`;
      if (meta.earpiece_type) return `Навушник: ${meta.earpiece_type} (${meta.side || 'вухо'})`;
      if (meta.speech_seconds) return `Тривалість мови: ${meta.speech_seconds}с`;
    } catch {}
    return '';
  }
}