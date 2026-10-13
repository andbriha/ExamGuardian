import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ExamService, ExamDto } from '../../../services/exam.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';

export interface CompletedExamView {
  id: string;
  title: string;
  groupName: string;
  date: string;
  violationsCount: number;
  avgScore: number;
  studentName?: string;
  status: 'passed' | 'failed' | 'aborted';
  statusText: string;
  durationSec: number;
}

@Component({
  selector: 'app-reports-hub',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './reports-hub.html',
  styleUrl: './reports-hub.css'
})
export class ReportsHubComponent implements OnInit {
  completedExams: CompletedExamView[] = [];
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
    this.isLoading = true;
    this.sessionService.getAll().subscribe({
      next: (sessions) => {
        this.examService.getAll().subscribe({
          next: (exams) => {
            this.buildViews(sessions, exams);
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.buildViews(sessions, []);
            this.isLoading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: () => {
        // Fallback: load exams if sessions endpoint returns empty
        this.examService.getAll().subscribe({
          next: (exams) => {
            this.completedExams = exams.map(e => ({
              id: e.id.toString(),
              title: e.title,
              groupName: e.description || 'Основна група',
              date: new Date().toLocaleDateString('uk-UA'),
              violationsCount: 0,
              avgScore: 0,
              studentName: 'Загальний звіт',
              status: 'aborted',
              statusText: 'Немає сесій',
              durationSec: 0
            }));
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: (err) => {
            this.isLoading = false;
            this.errorMessage = 'Не вдалося завантажити архів звітів: ' + (err.error?.message ?? err.message);
            this.cdr.detectChanges();
          }
        });
      }
    });
  }

  private buildViews(sessions: ExamSessionDto[], exams: ExamDto[]) {
    if (sessions && sessions.length > 0) {
      this.completedExams = sessions.map(s => {
        let durationSec = 0;
        if (s.startTime && s.endTime) {
          durationSec = Math.round((new Date(s.endTime).getTime() - new Date(s.startTime).getTime()) / 1000);
        }

        const isFinished = s.isFinished === true;
        const score = s.score ?? 0;

        let status: 'passed' | 'failed' | 'aborted' = 'aborted';
        let statusText = 'Перервано / Не пройдено';

        // Валідація: якщо сесія тривала менше 20 сек і бал 0 (студент вилетів на камері чи технічній перевірці),
        // або сесія взагалі не позначена як finished — іспит НЕ вважається пройденим!
        if (!isFinished) {
          status = 'aborted';
          statusText = 'Перервано (сесія не завершена)';
        } else if (durationSec < 20 && score === 0) {
          status = 'aborted';
          statusText = 'Не пройдено (збій камери / перервано)';
        } else if (score >= 60) {
          status = 'passed';
          statusText = 'Складено успішно';
        } else {
          status = 'failed';
          statusText = 'Не складено (недостатній бал)';
        }

        return {
          id: s.id.toString(),
          title: s.examTitle || `Іспит #${s.examId}`,
          groupName: s.studentName ? `Студент: ${s.studentName}` : 'Сесія тестування',
          date: s.startTime ? new Date(s.startTime).toLocaleDateString('uk-UA') : new Date().toLocaleDateString('uk-UA'),
          violationsCount: s.violationCount,
          avgScore: score,
          studentName: s.studentName,
          status,
          statusText,
          durationSec
        };
      });
    } else {
      this.completedExams = [];
    }
  }

  downloadReport = (id: string) => {
    const report = this.completedExams.find(r => r.id === id);
    const title = report ? report.title : `Іспит #${id}`;
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
          <title>Офіційний звіт Online Proctoring System - ${title}</title>
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
            <div class="row"><span class="label">Студент:</span><span>${report?.studentName || 'Не вказано'}</span></div>
            <div class="row"><span class="label">Дата:</span><span>${report?.date || new Date().toLocaleDateString('uk-UA')}</span></div>
            <div class="row"><span class="label">Тривалість сесії:</span><span>${report?.durationSec ?? 0} сек</span></div>
            <div class="row"><span class="label">Результат тестування:</span><span><strong>${report?.avgScore ?? 0} / 100</strong></span></div>
            <div class="row"><span class="label">Підсумок проходження:</span>
              <span class="badge ${statusBadgeClass}">
                ${report?.statusText}
              </span>
            </div>
            <div class="row"><span class="label">Порушень прокторингу (ШІ / Процеси):</span>
              <span class="badge ${(report?.violationsCount ?? 0) > 0 ? 'badge-warn' : 'badge-ok'}">
                ${report?.violationsCount ?? 0} зафіксовано
              </span>
            </div>
            <div class="row"><span class="label">Статус валідації:</span>
              <span>${isAborted ? 'Не зараховано (сесія перервана або не пройдена)' : (report?.violationsCount ?? 0) === 0 ? 'Успішно підтверджено без порушень' : 'Потребує повторного перегляду екзаменатором'}</span>
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