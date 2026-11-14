import { observationPriority, priorityLabel, episodeLabel, decisionLabel } from '../../../services/observation-presentation';
import { findEvidenceChunk } from '../../../services/evidence-timeline';
import { Component, OnInit, ViewChild, ElementRef, ChangeDetectorRef, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';

import { ViolationService, ViolationAlertDto, VideoChunkDto } from '../../../services/violation.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';
import { Subject, takeUntil } from 'rxjs';
import { ExamService } from '../../../services/exam.service';

@Component({
  selector: 'app-violation-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './violation-detail.html',
  styleUrls: ['./violation-detail.css']
})
export class ViolationDetailComponent implements OnInit, OnDestroy {
  observationPriority = observationPriority;
  priorityLabel = priorityLabel;
  episodeLabel = episodeLabel;
  decisionLabel = decisionLabel;

  @ViewChild('videoPlayer') videoRef?: ElementRef<HTMLVideoElement>;
  readonly Math = Math;

  private readonly destroyed=new Subject<void>();
  evidenceImageUrl = "";
  evidenceError = "";
  private pendingSeek: number | null = null;
  private videoRecoveryAttempted = false;
  private resumeAfterRecovery: boolean | null = null;
  violationId: string = '';
  currentViolation: ViolationAlertDto | null = null;
  get speechTranscript(): string {
    try {
      const metadata = JSON.parse(this.currentViolation?.metadata || '{}');
      return typeof metadata?.transcript === 'string' ? metadata.transcript : '';
    } catch { return ''; }
  }
  sessionViolations: ViolationAlertDto[] = [];
  videoChunks: VideoChunkDto[] = [];
  selectedChunkIndex: number = 1;
  currentVideoUrl: string = '';
  
  currentTime: number = 0;
  duration: number = 0;
  isPlaying: boolean = false;
  playbackRate: number = 1.0;
  
  isLoadingVideo: boolean = false;
  hasVideo: boolean = false;
  activeMarker: ViolationAlertDto | null = null;

  studentInfo = {
    name: 'Завантаження...',
    initials: 'СІ',
    login: 'student',
    examName: 'Іспит',
    group: 'Групу не вказано'
  };

  constructor(
    private route: ActivatedRoute,
    private location: Location,
    private violationService: ViolationService,
    private sessionService: SessionService,
    private examService: ExamService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.violationId = this.route.snapshot.paramMap.get('id') || '';
    this.loadData();
  }

  ngOnDestroy(): void { this.destroyed.next(); this.destroyed.complete(); if(this.evidenceImageUrl) URL.revokeObjectURL(this.evidenceImageUrl); }

  loadData(): void {
    const numId = parseInt(this.violationId, 10);
    if (isNaN(numId)) return;

    this.violationService.getById(numId).pipe(takeUntil(this.destroyed)).subscribe({
      next: (vio) => {
        this.selectObservation(vio);
        this.loadSessionData(vio.examSessionId, vio);
      },
      error: (err) => {
        this.reviewError = 'Не вдалося завантажити спостереження. Оновіть сторінку.'; this.cdr.detectChanges();
      }
    });
  }

  private loadSessionData(sessionId: number, targetViolation: ViolationAlertDto): void {
    // 1. Load all violations for this session to populate timeline markers
    this.violationService.getBySession(sessionId).pipe(takeUntil(this.destroyed)).subscribe({
      next: (violations) => {
        this.sessionViolations = violations || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.sessionViolations = [targetViolation];
      }
    });

    // 2. Load session info to get student name and exam title
    this.sessionService.getById(sessionId).pipe(takeUntil(this.destroyed)).subscribe({
      next: (session: ExamSessionDto) => {
        this.sessionExamId = session.examId;
        const studentName = session.studentName || `Студент #${session.studentId.substring(0, 8)}`;
        const initials = studentName.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase() || 'СТ';
        this.studentInfo = {
          name: studentName,
          initials,
          login: session.studentId,
          examName: session.examTitle || `Іспит #${session.examId}`,
          group: 'Групу не вказано'
        };
        this.cdr.detectChanges();
      },
      error: () => {
        this.studentInfo = {
          name: `Сесія #${sessionId}`,
          initials: 'СЕ',
          login: `session_${sessionId}`,
          examName: `Іспит сесії #${sessionId}`,
          group: 'Групу не вказано'
        };
      }
    });

    // 3. Load video chunks for this session
    this.loadVideoChunks(sessionId, targetViolation.relativeTimestampSeconds);
  }

  selectedStream: 'camera' | 'screen' = 'camera';
  private videoRequest = 0;
  changeStream(stream: 'camera' | 'screen'): void {
    const current = this.videoChunks.find(c => c.chunkIndex === this.selectedChunkIndex);
    const at = (current?.metadata?.startOffsetSeconds ?? 0) + this.currentTime;
    this.selectedStream = stream;
    if (this.currentViolation) this.loadVideoChunks(this.currentViolation.examSessionId, at);
  }
  private loadVideoChunks(sessionId: number, targetTimestampSec: number): void {
    const request = ++this.videoRequest;
    this.hasVideo = false; this.currentVideoUrl = ''; this.videoChunks = []; this.evidenceError = '';
    this.pendingSeek = null; this.videoRecoveryAttempted = false; this.currentTime = 0; this.duration = 0;
    this.isLoadingVideo = true;
    this.violationService.getVideoChunks(sessionId, this.selectedStream).pipe(takeUntil(this.destroyed)).subscribe({
      next: (chunks) => {
        if (request !== this.videoRequest) return;
        this.isLoadingVideo = false;
        if (chunks && chunks.length > 0) {
          this.videoChunks = chunks;
          this.hasVideo = true;

          const target = findEvidenceChunk(chunks, targetTimestampSec);
          this.selectedChunkIndex = target?.chunkIndex ?? chunks[0].chunkIndex;
          this.currentVideoUrl = (target ?? chunks[0]).url;
          this.pendingSeek = target ? targetTimestampSec - target.metadata!.startOffsetSeconds! : null;
          if (!target) this.evidenceError = 'Відео для цього моменту відсутнє або його час невідомий.';
        } else {
          this.hasVideo = false;
        }
        this.cdr.detectChanges();
      },
      error: () => {
        if (request !== this.videoRequest) return;
        this.evidenceError = 'Не вдалося завантажити список записів.';
        this.isLoadingVideo = false;
        this.hasVideo = false;
        this.cdr.detectChanges();
      }
    });
  }

  selectChunk(chunkIndex: number): void {
    const chunk = this.videoChunks.find(c => c.chunkIndex === chunkIndex);
    if (!chunk) return;
    this.selectedChunkIndex = chunkIndex;
    this.currentVideoUrl = chunk.url;
    this.currentTime = 0; this.duration = 0;
    this.pendingSeek = null; this.videoRecoveryAttempted = false; this.resumeAfterRecovery = null;
    this.activeMarker = null;
    this.cdr.detectChanges();
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
    const sessionId = this.currentViolation?.examSessionId;
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

  private evidenceRequest = 0;
  sessionExamId: number | null = null;
  private drafts = new Map<number, string>();
  reviewSuccess = '';
  selectedDecision: 'Confirmed' | 'Dismissed' | 'NoViolation' | 'InsufficientEvidence' | '' = '';
  selectObservation(v: ViolationAlertDto): void {
    if (this.reviewing) return;
    if (this.currentViolation?.id === v.id) return;
    if (this.currentViolation) this.drafts.set(this.currentViolation.id, this.reviewNotes);
    const request = ++this.evidenceRequest;
    this.location.replaceState('/violation/' + v.id);
    this.currentViolation = v; this.violationId = String(v.id); this.activeMarker = v;
    this.reviewNotes = this.drafts.get(v.id) ?? v.reviewNotes ?? '';
    this.selectedDecision = ''; this.reviewError = ''; this.reviewSuccess = '';
    if (this.evidenceImageUrl) URL.revokeObjectURL(this.evidenceImageUrl);
    this.evidenceImageUrl = '';
    if (v.eventId) this.violationService.getEvidence(v.eventId).pipe(takeUntil(this.destroyed)).subscribe({
      next: blob => { if (request !== this.evidenceRequest) return; this.evidenceImageUrl = URL.createObjectURL(blob); this.cdr.detectChanges(); },
      error: () => { if (request === this.evidenceRequest) { this.evidenceError = 'Кадр спостереження недоступний. Перевірте запис.'; this.cdr.detectChanges(); } }
    });
  }
  get reviewHistory(): { decision: string; notes: string; at: string; reviewerId: string }[] {
    try { const entries = JSON.parse(this.currentViolation?.reviewHistoryJson || '[]'); return Array.isArray(entries) ? entries.slice().reverse() : []; } catch { return []; }
  }
  reloadObservation(): void {
    const target = this.currentViolation;
    if (!target || this.reviewing) return;
    this.violationService.getById(target.id).pipe(takeUntil(this.destroyed)).subscribe({next: fresh => {
      if (this.currentViolation !== target) return;
      this.drafts.set(target.id, this.reviewNotes); this.currentViolation = null;
      this.seekToViolation(fresh); this.cdr.detectChanges();
    },error: () => { this.reviewError = 'Не вдалося оновити спостереження.'; this.cdr.detectChanges(); }});
  }
  saveSelected(next = false): void { if (this.selectedDecision) this.saveReview(this.selectedDecision, next); }
  seekToViolation(v: ViolationAlertDto): void {
    if (this.reviewing) return;
    this.selectObservation(v);
    this.evidenceError = '';
    const chunk = findEvidenceChunk(this.videoChunks, v.relativeTimestampSeconds);
    if (!chunk) { this.evidenceError = 'Фрагмент для цього моменту відсутній або час невідомий.'; return; }
    const offset = v.relativeTimestampSeconds - chunk.metadata!.startOffsetSeconds!;
    if (chunk.chunkIndex !== this.selectedChunkIndex) { this.selectChunk(chunk.chunkIndex); this.pendingSeek = offset; }
    else this.seekToSeconds(offset);
    this.activeMarker = v;
  }
  getViolationsInCurrentChunk(): ViolationAlertDto[] {
    const metadata = this.videoChunks.find(c => c.chunkIndex === this.selectedChunkIndex)?.metadata;
    if (typeof metadata?.startOffsetSeconds !== 'number' || typeof metadata.durationSeconds !== 'number') return [];
    return this.sessionViolations.filter(v => v.relativeTimestampSeconds >= metadata.startOffsetSeconds! && v.relativeTimestampSeconds < metadata.startOffsetSeconds! + metadata.durationSeconds!);
  }
  getMarkerPositionPercent(v: ViolationAlertDto): number {
    const start = this.videoChunks.find(c => c.chunkIndex === this.selectedChunkIndex)?.metadata?.startOffsetSeconds;
    return typeof start === 'number' ? Math.max(0,Math.min(100,(v.relativeTimestampSeconds-start)/this.duration*100)) : 0;
  }

  confidenceLabel(v: ViolationAlertDto): string { return ['cell_phone','book','laptop'].includes(v.violationType) ? `Оцінка детектора об’єкта: ${Math.round(v.confidence*100)}%` : 'Потрібна перевірка контексту'; }
  getViolationLabel(type: string): string {
    const map: Record<string, string> = {
      'identity_uncertain': 'Особа потребує повторної перевірки',
      'cell_phone': 'Предмет, схожий на телефон',
      'book': 'Предмет, схожий на книгу',
      'laptop': 'Предмет, схожий на ноутбук',
      'extra_person': 'Можлива додаткова людина в кадрі',
      'analysis_unavailable': 'Тривало недостатня якість кадру',
      'micro_earpiece': '🎧 Мікронавушник у вусі',
      'earphone_detected': '🎧 Навушники виявлено',
      'looking_away_left': '👀 Поворот голови ліворуч',
      'looking_away_right': '👀 Поворот голови праворуч',
      'looking_down': '⬇️ Нахил голови вниз',
      'looking_away': '👀 Відведення погляду',
      'multiple_faces': 'Виявлено кілька облич — перевірте контекст',
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

  formatTime(sec: number): string {
    const total = Math.floor(sec || 0);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  goBack(): void {
    this.location.back();
  }

  reviewNotes = '';
  reviewError = '';
  reviewing = false;
  confirmViolation(): void { this.saveReview('Confirmed'); }
  rejectViolation(): void { this.saveReview('Dismissed'); }
  noViolation(): void { this.saveReview('NoViolation'); }
  insufficientEvidence(): void { this.saveReview('InsufficientEvidence'); }
  private saveReview(decision: 'Confirmed' | 'Dismissed' | 'NoViolation' | 'InsufficientEvidence', advance = false): void {
    if (!this.currentViolation || this.reviewing) return;
    if (!this.reviewNotes.trim()) { this.reviewError = 'Поясніть рішення після перегляду контексту.'; return; }
    const target = this.currentViolation;
    this.reviewing = true;
    this.reviewError = '';
    this.violationService.markAsReviewed(this.currentViolation.id, decision, this.reviewNotes, this.currentViolation.reviewVersion).pipe(takeUntil(this.destroyed)).subscribe({
      next: () => {
        this.selectedDecision = '';
        this.reviewing = false; target.reviewDecision = decision; target.reviewNotes = this.reviewNotes;
        target.isReviewed = true;
        this.sessionViolations = this.sessionViolations.map(v => v.id === target.id ? target : v);
        this.reviewSuccess = `Рішення для спостереження #${target.id} збережено.`;
        if (advance) {
          const next = this.sessionViolations.find(v => v.id !== target.id && ['Pending','LegacyReviewed'].includes(v.reviewDecision));
          if (next) this.seekToViolation(next);
          else this.reviewSuccess += ' Нових спостережень у цій спробі немає.';
        }
        this.violationService.getById(target.id).pipe(takeUntil(this.destroyed)).subscribe({next: fresh => {
          Object.assign(target, fresh);
          this.cdr.detectChanges();
        }, error: () => {}});
        this.cdr.detectChanges();
      },
      error: error => { this.reviewing = false; this.reviewError = error.error?.message || 'Рішення не збережено. Перевірте з’єднання та спробуйте повторно.'; this.cdr.detectChanges(); }
    });
  }
}
