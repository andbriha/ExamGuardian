import { RouterModule } from '@angular/router';
import { Component, OnInit, ChangeDetectorRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GroupResultsComponent } from '../group-results/group-results';
import { GradeExportComponent } from '../grade-export/grade-export';

import { ExamService, ExamDto } from '../../../services/exam.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';

export interface CompletedExamView {
  id: string;
  title: string;
  groupName: string;
  date: string;
  violationsCount: number;
  avgScore: number;
  scoreCalculated?: boolean;
  studentName?: string;
  status: 'passed' | 'failed' | 'aborted' | 'unknown';
  statusText: string;
  durationSec: number;
  examId?: number;
  integrityVerdict?: string;
  integrityNotes?: string;
  evidenceStatus?: string;
  identityVerificationSkipped?: boolean;
}

@Component({
  selector: 'app-reports-hub',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, GradeExportComponent, GroupResultsComponent],
  templateUrl: './reports-hub.html',
  styleUrl: './reports-hub.css'
})
export class ReportsHubComponent implements OnInit {
  @ViewChild(GradeExportComponent) gradeExport?: GradeExportComponent;
  completedExams: CompletedExamView[] = [];
  exams: ExamDto[] = [];
  sessions: ExamSessionDto[] = [];
  review: ExamSessionDto | null = null;
  reviewLoading = false;
  reviewSaving = false;
  reviewVerdict = '';
  reviewNotes = '';
  reviewError = '';
  reviewSuccess = '';
  reviewHistory: { verdict: string; notes: string; at: string; reviewerId: string }[] = [];

  openReview(id: string) {
    if (this.reviewSaving || this.reviewLoading) return;
    this.reviewSuccess = '';
    const sameAttempt = this.review?.id === Number(id);
    if (!sameAttempt) { this.reviewNotes = ''; this.reviewVerdict = ''; }
    this.review = null; this.reviewLoading = true; this.reviewError = '';
    this.sessionService.getById(Number(id)).subscribe({
      next: session => {
        this.review = session; this.reviewLoading = false;
        setTimeout(() => document.getElementById('review-heading')?.scrollIntoView({behavior: 'smooth', block: 'start'}));
        try { const history = JSON.parse(session.integrityHistoryJson || '[]'); this.reviewHistory = Array.isArray(history) ? history.slice().reverse() : []; }
        catch { this.reviewHistory = []; }
        this.cdr.markForCheck();
      },
      error: () => { this.reviewError = 'Не вдалося оновити стан спроби.'; this.reviewLoading = false; this.cdr.markForCheck(); }
    });
  }
  closeReview() {
    this.review = null;
    document.getElementById('group-results-heading')?.scrollIntoView({behavior: 'smooth', block: 'start'});
  }
  evidenceLabel(status: string): string {
    return ({Delivered:'Доставлено повністю',Pending:'Очікується доставка',Partial:'Доставлено частково',Unknown:'Стан невідомий',Expired:'Строк зберігання минув'} as Record<string,string>)[status] || status;
  }
  reviewBlock(verdict: string): string {
    if (!['Accepted','Rejected','Inconclusive'].includes(verdict)) return 'Оберіть висновок після перевірки матеріалів.';
    const s = this.review;
    if (verdict === 'Accepted' && s?.identityVerificationSkipped) return 'DEV: перевірку особи пропущено. Результат не можна підтвердити або експортувати.';
    if (!s?.isFinished) return 'Спочатку завершіть спробу.';
    if (verdict === 'Accepted' && s.evidenceStatus !== 'Delivered') return 'Дочекайтеся повної доставки доказів або оберіть «Недостатньо доказів».';
    if (verdict === 'Accepted' && ((s.pendingObservationCount ?? 0) + (s.confirmedObservationCount ?? 0) > 0)) return 'Перевірте всі сигнали: для прийняття потрібні рішення «Помилка розпізнавання» або «Подія була, порушення немає».';
    if (verdict === 'Rejected' && !(s.confirmedObservationCount! > 0)) return 'Спочатку підтвердьте хоча б один сигнал у матеріалах сесії.';
    return '';
  }
  saveReview() {
    if (!this.review || this.reviewSaving || this.reviewBlock(this.reviewVerdict) || !this.reviewNotes.trim()) return;
    this.reviewSaving = true; this.reviewError = '';
    this.sessionService.review(this.review.id, this.reviewVerdict, this.reviewNotes.trim()).subscribe({
      next: () => { this.reviewSaving = false; this.reviewSuccess = 'Рішення збережено.'; this.closeReview(); this.loadReports(); this.gradeExport?.refresh(); },
      error: error => { this.reviewSaving = false; this.reviewError = error.error?.message || 'Не вдалося зберегти рішення. Оновіть стан спроби.'; this.cdr.markForCheck(); }
    });
  }
  isLoading = true;
  errorMessage = '';

  constructor(
    private examService: ExamService,
    private sessionService: SessionService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadReports();
  }

  loadReports() {
    this.errorMessage = '';
    this.isLoading = true;
    this.sessionService.getAll().subscribe({
      next: (sessions) => {
        this.sessions = sessions;
        this.examService.getAll().subscribe({
          next: (exams) => {
            this.exams = exams;
            this.buildViews(sessions, exams);
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.exams = []; this.completedExams = []; this.sessions = [];
            this.errorMessage = 'Не вдалося завантажити іспити. Оновіть архів.';
            this.isLoading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: () => { this.sessions = []; this.completedExams = []; this.isLoading = false; this.errorMessage = 'Не вдалося отримати архів сесій. Спробуйте повторно.'; this.cdr.detectChanges(); }

    });
  }

  private buildViews(sessions: ExamSessionDto[], exams: ExamDto[]) {
    if (sessions && sessions.length > 0) {
      this.completedExams = sessions.filter(s => s.isFinished).map(s => {
        let durationSec = 0;
        if (s.startTime && s.endTime) {
          durationSec = Math.round((new Date(s.endTime).getTime() - new Date(s.startTime).getTime()) / 1000);
        }

        const isFinished = s.isFinished === true;
        const score = s.score ?? 0;

        let status: 'passed' | 'failed' | 'aborted' | 'unknown' = 'aborted';
        let statusText = 'Перервано / Не пройдено';

        // Валідація: якщо сесія тривала менше 20 сек і бал 0 (студент вилетів на камері чи технічній перевірці),
        // або сесія взагалі не позначена як finished — іспит НЕ вважається пройденим!
        if (!isFinished) {
          status = 'aborted';
          statusText = 'Перервано (сесія не завершена)';
        } else if (s.status === "TerminatedByProctor") {
          status = 'aborted';
          statusText = 'Припинено викладачем';
        } else if (s.scoreCalculated === false) {
          status = "unknown"; statusText="Історичний бал потребує перевірки";
        } else if (score >= (s.passingScoreAtStart ?? 60)) {
          status = 'passed';
          statusText = 'Порог бала досягнуто';
        } else {
          status = 'failed';
          statusText = 'Порог бала не досягнуто';
        }

        return {
          id: s.id.toString(),
          title: s.examTitle || `Іспит #${s.examId}`,
          groupName: s.studentName ? `Студент: ${s.studentName}` : 'Сесія тестування',
          date: s.startTime ? new Date(s.startTime).toLocaleDateString('uk-UA') : new Date().toLocaleDateString('uk-UA'),
          violationsCount: s.violationCount,
          avgScore: score, scoreCalculated: s.scoreCalculated,
          studentName: s.studentName,
          status,
          statusText,
          durationSec, identityVerificationSkipped: s.identityVerificationSkipped, examId: s.examId, integrityVerdict: s.integrityVerdict, integrityNotes: s.integrityNotes, evidenceStatus: s.evidenceStatus
        };
      });
    } else {
      this.completedExams = [];
    }
  }

  verdictLabel(verdict?: string): string { return ({UnderReview:'Очікує висновку',Accepted:'Перевірено: прийнято',Rejected:'Добросовісність не підтверджено',Inconclusive:'Недостатньо доказів'} as Record<string,string>)[verdict || 'UnderReview'] || 'Очікує висновку'; }
  downloadReport = (id: string) => {
    const report = this.completedExams.find(r => r.id === id);
    const escapeHtml = (v: string) => v.replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
    const title = escapeHtml(report ? report.title : `Іспит #${id}`);
    const student = report?.studentName ? `Учасник: ${report.studentName}\n` : '';
    const score = report ? `Бал: ${report.avgScore}/100\n` : '';
    const violations = report ? `Зафіксовано порушень ШІ: ${report.violationsCount}\n` : '';
    
    // Generate a printable window
    const printWindow = window.open('', '_blank');
    if (printWindow) {
      const isPassed = report?.status === 'passed';
      const isAborted = report?.status === 'aborted';
      const statusBadgeClass = isPassed ? 'badge-ok' : 'badge-warn';

      printWindow.document.write(`
        <!DOCTYPE html>
        <html>
        <head>
          <title>Звіт про спробу ExamGuardian - ${title}</title>
          <style>
            body { font-family: 'Segoe UI', Arial, sans-serif; padding: 40px; color: #1e293b; }
            h1 { color: #1a56db; margin-bottom: 8px; }
            .meta { color: #64748b; margin-bottom: 24px; font-size: 14px; }
            .card { border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin-bottom: 20px; }
            .row { display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #f1f5f9; }
            .row:last-child { border-bottom: none; }
            .label { font-weight: 600; color: #475569; }
            .badge { display: inline-block; padding: 4px 12px; border-radius: 999px; font-size: 12px; font-weight: 600; }
            .badge-ok { background: #dcfce7; color: #166534; }
            .badge-warn { background: #fee2e2; color: #991b1b; }
            .badge-abort { background: #f1f5f9; color: #475569; }
            @media print { button { display: none; } }
          </style>
        </head>
        <body>
          <h1>🛡️ Online Proctoring System &mdash; Протокол сесії іспиту</h1>
          <div class="meta">Згенеровано: ${new Date().toLocaleString('uk-UA')} &bull; Система автоматичного моніторингу</div>
          <div class="card">
            <div class="row"><span class="label">Назва дисципліни / іспиту:</span><span>${title}</span></div>
            <div class="row"><span class="label">Ідентифікатор сесії:</span><span>#OPS-${id}</span></div>
            <div class="row"><span class="label">Студент:</span><span>${escapeHtml(report?.studentName || 'Не вказано')}</span></div>
            <div class="row"><span class="label">Дата:</span><span>${report?.date || new Date().toLocaleDateString('uk-UA')}</span></div>
            <div class="row"><span class="label">Тривалість сесії:</span><span>${report?.durationSec ?? 0} сек</span></div>
            <div class="row"><span class="label">Результат тестування:</span><span><strong>${report?.scoreCalculated === false ? "Не розраховано" : (report?.avgScore ?? 0) + " / 100"}</strong></span></div>
            <div class="row"><span class="label">Підсумок проходження:</span>
              <span class="badge ${statusBadgeClass}">
                ${escapeHtml(report?.statusText || "")}
              </span>
            </div>
            <div class="row"><span class="label">Спостережень для перевірки:</span>
              <span class="badge ${(report?.violationsCount ?? 0) > 0 ? 'badge-warn' : 'badge-ok'}">
                ${report?.violationsCount ?? 0} зафіксовано
              </span>
            </div>
            <div class="row"><span class="label">Перевірка особи:</span><span>${report?.identityVerificationSkipped ? "DEV: пропущено, тестова спроба" : "Звичайний допуск"}</span></div>
            <div class="row"><span class="label">Висновок прокторингу:</span><span>${escapeHtml(this.verdictLabel(report?.integrityVerdict))}</span></div>
            <div class="row"><span class="label">Підстава висновку:</span><span>${escapeHtml(report?.integrityNotes || 'Висновок ще не надано')}</span></div>
            <div class="row"><span class="label">Доставка доказів:</span><span>${escapeHtml(report?.evidenceStatus || 'Unknown')}</span></div>
            <div class="row"><span class="label">Статус валідації:</span>
              <span>${isAborted ? 'Не зараховано (сесія перервана або не пройдена)' : (report?.violationsCount ?? 0) === 0 ? 'Автоматичних сигналів немає. Повнота моніторингу та висновок потребують перевірки' : 'Потребує повторного перегляду екзаменатором'}</span>
            </div>
          </div>
          <button onclick="window.print()" style="padding: 10px 20px; background: #1a56db; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 14px;">🖨️ Друк / Зберегти як PDF</button>
        </body>
        </html>
      `);
      printWindow.document.close();
    } else {
      alert(`Звіт для ${title}:\n${student}${score}${violations}Статус: ${report?.statusText}`);
    }
  };
}
