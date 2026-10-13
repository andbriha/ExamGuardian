import { Component, OnInit, ViewChild, ElementRef, ChangeDetectorRef } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';

import { ViolationService, ViolationAlertDto, VideoChunkDto } from '../../../services/violation.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';
import { ExamService } from '../../../services/exam.service';

@Component({
  selector: 'app-violation-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './violation-detail.html',
  styleUrls: ['./violation-detail.css']
})
export class ViolationDetailComponent implements OnInit {
  @ViewChild('videoPlayer') videoRef?: ElementRef<HTMLVideoElement>;
  readonly Math = Math;

  violationId: string = '';
  currentViolation: ViolationAlertDto | null = null;
  sessionViolations: ViolationAlertDto[] = [];
  videoChunks: VideoChunkDto[] = [];
  selectedChunkIndex: number = 1;
  currentVideoUrl: string = '';
  
  currentTime: number = 0;
  duration: number = 60;
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
    group: 'Група'
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
    this.violationId = this.route.snapshot.paramMap.get('id') || '1';
    this.loadData();
  }

  loadData(): void {
    const numId = parseInt(this.violationId, 10);
    if (isNaN(numId)) return;

    this.violationService.getById(numId).subscribe({
      next: (vio) => {
        this.currentViolation = vio;
        this.activeMarker = vio;
        this.loadSessionData(vio.examSessionId, vio);
      },
      error: (err) => {
        console.error('Failed to load violation:', err);
      }
    });
  }

  private loadSessionData(sessionId: number, targetViolation: ViolationAlertDto): void {
    // 1. Load all violations for this session to populate timeline markers
    this.violationService.getBySession(sessionId).subscribe({
      next: (violations) => {
        this.sessionViolations = violations || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.sessionViolations = [targetViolation];
      }
    });

    // 2. Load session info to get student name and exam title
    this.sessionService.getById(sessionId).subscribe({
      next: (session: ExamSessionDto) => {
        const studentName = session.studentName || `Студент #${session.studentId.substring(0, 8)}`;
        const initials = studentName.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase() || 'СТ';
        this.studentInfo = {
          name: studentName,
          initials,
          login: session.studentId,
          examName: session.examTitle || `Іспит #${session.examId}`,
          group: 'Основна група'
        };
        this.cdr.detectChanges();
      },
      error: () => {
        this.studentInfo = {
          name: `Сесія #${sessionId}`,
          initials: 'СЕ',
          login: `session_${sessionId}`,
          examName: `Іспит сесії #${sessionId}`,
          group: 'Основна група'
        };
      }
    });

    // 3. Load video chunks for this session
    this.loadVideoChunks(sessionId, targetViolation.relativeTimestampSeconds);
  }

  private loadVideoChunks(sessionId: number, targetTimestampSec: number): void {
    this.isLoadingVideo = true;
    this.violationService.getVideoChunks(sessionId).subscribe({
      next: (chunks) => {
        this.isLoadingVideo = false;
        if (chunks && chunks.length > 0) {
          this.videoChunks = chunks;
          this.hasVideo = true;

          // Compute target chunk index based on 60s per chunk
          const targetChunk = Math.floor(targetTimestampSec / 60) + 1;
          const chunkExists = chunks.some(c => c.chunkIndex === targetChunk);
          this.selectedChunkIndex = chunkExists ? targetChunk : chunks[0].chunkIndex;

          const activeChunkObj = chunks.find(c => c.chunkIndex === this.selectedChunkIndex) || chunks[0];
          this.currentVideoUrl = activeChunkObj.url;
        } else {
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
  }

  selectChunk(chunkIndex: number): void {
    const chunk = this.videoChunks.find(c => c.chunkIndex === chunkIndex);
    if (!chunk) return;
    this.selectedChunkIndex = chunkIndex;
    this.currentVideoUrl = chunk.url;
    this.currentTime = 0;
    this.activeMarker = null;
    this.cdr.detectChanges();
  }

  onMetadataLoaded(): void {
    if (this.videoRef?.nativeElement) {
      this.duration = this.videoRef.nativeElement.duration || 60;
      this.videoRef.nativeElement.playbackRate = this.playbackRate;

      // If opening violation initially, seek directly to its second inside the chunk
      if (this.currentViolation) {
        const offset = this.currentViolation.relativeTimestampSeconds % 60;
        this.seekToSeconds(offset);
      }
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
    this.activeMarker = v;
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

  formatTime(sec: number): string {
    const total = Math.floor(sec || 0);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  goBack(): void {
    this.location.back();
  }

  confirmViolation(): void {
    if (!this.currentViolation) return;
    this.violationService.markAsReviewed(this.currentViolation.id).subscribe({
      next: () => {
        alert(`Порушення #${this.currentViolation?.id} успішно підтверджено.`);
        this.goBack();
      },
      error: () => {
        alert(`Порушення #${this.currentViolation?.id} підтверджено.`);
        this.goBack();
      }
    });
  }

  sendWarning(): void {
    alert(`Попередження надіслано студенту: ${this.studentInfo.name}`);
  }

  rejectViolation(): void {
    if (!this.currentViolation) return;
    this.violationService.markAsReviewed(this.currentViolation.id).subscribe({
      next: () => {
        alert(`Порушення #${this.currentViolation?.id} відхилено як хибне спрацювання ШІ.`);
        this.goBack();
      },
      error: () => {
        alert(`Порушення #${this.currentViolation?.id} відхилено.`);
        this.goBack();
      }
    });
  }
}