import { findEvidenceChunk } from '../../../services/evidence-timeline';
import { Component, OnInit, OnDestroy, ViewChild, ElementRef, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';

import { ExamService, ExamDto } from '../../../services/exam.service';
import { StudentService, StudentDto } from '../../../services/student.service';
import { ViolationService, ViolationAlertDto, VideoChunkDto } from '../../../services/violation.service';
import { Subject, takeUntil } from 'rxjs';
import { SessionService, ExamSessionDto } from '../../../services/session.service';

export interface StudentSessionView {
  sessionId: number;
  studentId: string;
  name: string;
  progress: string;
  status: 'ok' | 'warn' | 'offline';
  violationCount: number;
  analysisStatus: string;
  evidenceStatus: string;
  audioStatus: string;
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
  duration: number = 0;
  isPlaying: boolean = false;
  playbackRate: number = 1.0;
  hasVideo: boolean = false;
  isLoadingVideo: boolean = false;
  activeViolationMarker: ViolationAlertDto | null = null;
  
  evidenceError = '';
  selectedStream: 'camera' | 'screen' = 'camera';
  private pendingSeek: number | null = null;
  private videoRecoveryAttempted = false;
  private resumeAfterRecovery: boolean | null = null;
  previewUrl = '';
  previewCapturedAt = '';
  private previewTimer: any = null;
  private previewPending = false;
  private initialSessionId: number | null = null;
  isLoading = true;
  private readonly destroyed = new Subject<void>();
  private selectionGeneration = 0;
  private sessionsPending = false;
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
    this.initialSessionId = Number(this.route.snapshot.queryParamMap.get("session")) || null;
    this.loadData();
    this.previewTimer = setInterval(() => this.refreshPreview(), 2000);
    this.pollInterval = setInterval(() => this.loadSessions(Number(this.examId)), 8000);
  }

  ngOnDestroy() {
    this.destroyed.next(); this.destroyed.complete();
    if (this.previewTimer) clearInterval(this.previewTimer);
    if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
    }
  }

  loadData() {
    this.isLoading = true;
    const numId = this.examId ? parseInt(this.examId, 10) : NaN;
    if (!isNaN(numId)) {
      this.examService.getById(numId).pipe(takeUntil(this.destroyed)).subscribe({
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
    if (this.sessionsPending) return;
    this.sessionsPending = true;
    this.sessionService.getByExam(examIdNum).pipe(takeUntil(this.destroyed)).subscribe({
      next: (sessions) => {
        this.sessionsPending = false;
        const relevant = examIdNum > 0 ? sessions.filter(s => s.examId === examIdNum) : sessions;
        if (relevant && relevant.length > 0) {
          this.students = relevant.map((s, idx) => ({
            sessionId: s.id, studentId: s.studentId,
            name: s.studentName || `Студент #${s.studentId.substring(0, 8)}`,
            progress: s.isFinished ? 'Завершено' : 'Складання',
            status: !s.lastSeenAt || Date.now() - Date.parse(s.lastSeenAt) > 30000 ? 'offline' : (s.pendingObservationCount ?? s.violationCount)+(s.confirmedObservationCount ?? 0) > 0 ? 'warn' : 'ok',
            violationCount: s.violationCount,
            analysisStatus: s.isFinished && s.lastAnalysisAt ? "Кадри надходили; перевірте записи" : s.lastAnalysisAt && Date.now()-Date.parse(s.lastAnalysisAt) < 30000 ? "Камера аналізується" : "Аналіз камери не підтверджено",
            evidenceStatus: s.evidenceStatus,
            audioStatus: s.isFinished && s.lastAudioAt ? "Аудіо надходило; перевірте записи" : s.lastAudioAt && Date.now()-Date.parse(s.lastAudioAt) < 30000 ? "Аудіо аналізується" : "Аудіоаналіз не підтверджено",
            score: s.scoreCalculated===false ? undefined : s.score
          }));
          if (this.selectedSession) {
            this.selectedSession = this.students.find(s => s.sessionId === this.selectedSession!.sessionId) || null;
            this.refreshSelectedEvidence();
          }
          // Auto select first session
          if (!this.selectedSession && this.students.length > 0) {
            this.selectSession(this.students.find(s => s.sessionId === this.initialSessionId) || this.students[0]);
          }
        } else {
          this.students = [];
        }
        this.refreshAlerts();
      },
      error: () => {
        this.sessionsPending = false;
        this.evidenceError = "Дані сесій тимчасово недоступні.";
        this.students = [];
        this.refreshAlerts();
      }
    });
  }

  selectSession(st: StudentSessionView, target?: ViolationAlertDto) {
    this.selectionGeneration++;
    const generation = this.selectionGeneration;
    if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
    this.previewUrl = ""; this.previewCapturedAt = "";
    this.pendingSeek = null; this.evidenceError = ""; this.videoChunks = [];
    this.currentTime=0; this.duration=0; this.isPlaying=false;
    this.videoRecoveryAttempted=false; this.resumeAfterRecovery=null;
    this.selectedSession = st;
    this.isLoadingVideo = true;
    this.hasVideo = false;
    this.currentVideoUrl = '';
    this.sessionViolations = [];
    this.activeViolationMarker = null;

    // Load video chunks for this session
    this.violationService.getVideoChunks(st.sessionId, this.selectedStream).pipe(takeUntil(this.destroyed)).subscribe({
      next: (chunks) => {
        if (this.selectedSession?.sessionId !== st.sessionId || this.selectionGeneration !== generation) return;
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
        if (target) this.seekToViolation(target);
        this.cdr.detectChanges();
      },
      error: () => {
        if (this.selectionGeneration !== generation) return;
        this.isLoadingVideo = false;
        this.hasVideo = false;
        this.cdr.detectChanges();
      }
    });

    // Load violations for this session
    this.violationService.getBySession(st.sessionId).pipe(takeUntil(this.destroyed)).subscribe({
      next: (vios) => {
        if (this.selectedSession?.sessionId !== st.sessionId || this.selectionGeneration !== generation) return;
        this.sessionViolations = vios || [];
        this.cdr.detectChanges();
      },
      error: () => {
        if (this.selectionGeneration !== generation) return;
        this.sessionViolations = [];
        this.cdr.detectChanges();
      }
    });
  }

  private refreshSelectedEvidence(): void {
    const id=this.selectedSession?.sessionId; const generation=this.selectionGeneration; const stream=this.selectedStream;
    if (!id) return;
    this.violationService.getVideoChunks(id,stream).pipe(takeUntil(this.destroyed)).subscribe({next: chunks => {
      if (generation !== this.selectionGeneration) return;
      this.videoChunks=chunks;
      if (!this.hasVideo && chunks.length) { this.hasVideo=true; this.selectChunk(chunks[0].chunkIndex); }
      this.cdr.markForCheck();
    },error: () => {}});
    this.violationService.getBySession(id).pipe(takeUntil(this.destroyed)).subscribe({next: events => {
      if (generation !== this.selectionGeneration) return;
      this.sessionViolations=events; this.cdr.markForCheck();
    },error: () => {}});
  }

  selectChunk(chunkIndex: number) {
    const chunk = this.videoChunks.find(c => c.chunkIndex === chunkIndex);
    if (!chunk) return;
    this.selectedChunkIndex = chunkIndex;
    this.currentVideoUrl = chunk.url;
    this.currentTime = 0; this.duration = 0;
    this.pendingSeek = null; this.videoRecoveryAttempted = false; this.resumeAfterRecovery = null;
    this.activeViolationMarker = null;
    this.cdr.detectChanges();
  }

  refreshAlerts() {
    this.violationService.getUnreviewed().pipe(takeUntil(this.destroyed)).subscribe({
      next: (unreviewed) => {
        this.isLoading = false;
        if (unreviewed && unreviewed.length > 0) {
          this.alerts = unreviewed.filter(u => this.students.some(s => s.sessionId === u.examSessionId)).map(u => ({
            id: u.id.toString(),
            sessionId: u.examSessionId,
            time: new Date(u.timestamp).toLocaleTimeString(),
            type: this.getViolationLabel(u.violationType),
            studentName: this.students.find(s => s.sessionId === u.examSessionId)?.name || `Сесія #${u.examSessionId}`,
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
        this.alerts = [];
        this.evidenceError = "Список подій тимчасово недоступний.";
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onMetadataLoaded(): void {
    const video = this.videoRef?.nativeElement;
    if (!video) return;
    this.duration = Number.isFinite(video.duration) ? video.duration : 0;
    video.playbackRate = this.playbackRate;
    if (this.pendingSeek !== null) { const seek=this.pendingSeek; this.pendingSeek=null; this.seekToSeconds(seek); }
    if (this.resumeAfterRecovery === false) { video.pause(); this.isPlaying=false; }
    this.resumeAfterRecovery=null;
    this.cdr.detectChanges();
  }

  onVideoError(): void {
    const sessionId = this.selectedSession?.sessionId;
    if (!sessionId || this.videoRecoveryAttempted) {
      this.evidenceError='Не вдалося відтворити відео. Перевірте доступність запису та з’єднання.';
      this.cdr.markForCheck(); return;
    }
    this.videoRecoveryAttempted=true;
    const oldUrl=this.currentVideoUrl, index=this.selectedChunkIndex;
    const offset=this.videoRef?.nativeElement.currentTime ?? this.currentTime;
    const resume=this.isPlaying;
    this.violationService.getVideoChunks(sessionId, this.selectedStream).pipe(takeUntil(this.destroyed)).subscribe({next: chunks => {
      if (this.currentVideoUrl!==oldUrl || this.selectedChunkIndex!==index) return;
      const renewed=chunks.find(c => c.chunkIndex===index);
      if (!renewed || renewed.url===oldUrl) {
        this.evidenceError='Відео недоступне або пошкоджене.'; this.cdr.markForCheck(); return;
      }
      this.videoChunks=chunks; this.pendingSeek=offset; this.resumeAfterRecovery=resume;
      this.duration=0; this.currentVideoUrl=renewed.url; this.evidenceError=''; this.cdr.detectChanges();
    },error: () => { if (this.currentVideoUrl===oldUrl) { this.evidenceError='Не вдалося поновити доступ до відео.'; this.cdr.markForCheck(); } }});
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
    if (this.duration<=0) { this.pendingSeek=sec; return; }
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
    const chunk = findEvidenceChunk(this.videoChunks, v.relativeTimestampSeconds);
    if (!chunk) { this.evidenceError = 'Відео для цього моменту відсутнє або часові дані невідомі. Перегляньте стоп-кадр та стан доставки.'; return; }
    this.evidenceError = '';
    const offset = v.relativeTimestampSeconds - chunk.metadata!.startOffsetSeconds!;
    if (chunk.chunkIndex !== this.selectedChunkIndex) { this.selectChunk(chunk.chunkIndex); this.pendingSeek = offset; }
    else this.seekToSeconds(offset);
    this.activeViolationMarker = v;
  }
  onAlertClicked(alert: AlertView): void {
    const session = this.students.find(s => s.sessionId === alert.sessionId);
    if (!session) return;
    if (this.selectedSession?.sessionId !== alert.sessionId) this.selectSession(session, alert.raw);
    else this.seekToViolation(alert.raw);
  }
  getViolationsInCurrentChunk(): ViolationAlertDto[] {
    const chunk = this.videoChunks.find(c => c.chunkIndex === this.selectedChunkIndex);
    if (!chunk?.metadata || typeof chunk.metadata.startOffsetSeconds !== 'number' || typeof chunk.metadata.durationSeconds !== 'number') return [];
    const start = chunk.metadata.startOffsetSeconds;
    return this.sessionViolations.filter(v => v.relativeTimestampSeconds >= start && v.relativeTimestampSeconds < start + chunk.metadata!.durationSeconds!);
  }
  getMarkerPositionPercent(v: ViolationAlertDto): number {
    const start = this.videoChunks.find(c => c.chunkIndex === this.selectedChunkIndex)?.metadata?.startOffsetSeconds;
    return typeof start === 'number' ? Math.max(0, Math.min(100, (v.relativeTimestampSeconds - start)/this.duration*100)) : 0;
  }
  changeStream(stream: 'camera' | 'screen'): void { this.selectedStream = stream; if (this.selectedSession) this.selectSession(this.selectedSession); }
  evidenceLabel(status: string): string { return ({Pending: 'Очікування передачі',Delivered: 'Записи доставлено',Partial: 'Неповні записи',Unknown: 'Повнота невідома',Expired: 'Строк зберігання минув'} as Record<string,string>)[status] || 'Повнота невідома'; }
  terminateSelected(): void {
    if (!this.selectedSession || !confirm('Припинити цю спробу? Відповіді та докази залишаться у системі.')) return;
    this.sessionService.terminate(this.selectedSession.sessionId).pipe(takeUntil(this.destroyed)).subscribe({next: () => this.loadSessions(Number(this.examId)),error: () => { this.evidenceError = 'Не вдалося припинити спробу.'; this.cdr.detectChanges(); }});
  }
  refreshPreview(): void {
    const id = this.selectedSession?.sessionId;
    if (!id || this.previewPending) return;
    this.previewPending = true;
    this.sessionService.getPreview(id).pipe(takeUntil(this.destroyed)).subscribe({
      next: response => {
        this.previewPending = false;
        if (this.selectedSession?.sessionId !== id || !response.body) return;
        if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
        this.previewUrl = URL.createObjectURL(response.body);
        this.previewCapturedAt = response.headers.get('X-Captured-At') || '';
        this.cdr.detectChanges();
      }, error: () => { this.previewPending = false; }
    });
  }

  formatTime(sec: number): string {
    const total = Math.floor(sec || 0);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  confidenceLabel(v: ViolationAlertDto): string { return ['cell_phone','book','laptop'].includes(v.violationType) ? `Оцінка детектора об’єкта: ${Math.round(v.confidence*100)}%` : 'Потрібна перевірка контексту'; }
  getViolationLabel(type: string): string {
    const map: Record<string, string> = {
      'identity_uncertain': 'Особа потребує повторної перевірки',
      'cell_phone': '📱 Телефон у руках / на столі',
      'micro_earpiece': '🎧 Мікронавушник у вусі',
      'earphone_detected': '🎧 Навушники виявлено',
      'looking_away_left': '👀 Поворот голови ліворуч',
      'looking_away_right': '👀 Поворот голови праворуч',
      'looking_down': '⬇️ Нахил голови вниз',
      'looking_away': '👀 Відведення погляду',
      'multiple_faces': '👥 Стороння особа в кадрі',
      'face_missing': '🚫 Обличчя не розпізнано у кадрі',
      'voice_detected': '🎙️ Мовленнєва активність',
      'voice_activity': '🎙️ Мовленнєва активність',
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