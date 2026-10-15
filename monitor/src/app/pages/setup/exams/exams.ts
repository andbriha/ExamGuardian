import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { ExamService, ExamDto } from '../../../services/exam.service';

export interface ExamSummary {
  id: number;
  title: string;
  description: string;
  startTime: string;
  timeZoneId?: string;
  durationMinutes: number;
  questionsCount: number;
  status: 'upcoming' | 'active' | 'completed';
}

@Component({
  selector: 'app-exams',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './exams-component.html',
  styleUrl: './exams-component.css',
})
export class ExamsComponent implements OnInit, OnDestroy {
  exams: ExamSummary[] = [];
  private timer?: ReturnType<typeof setInterval>;
  private clockOffset = 0;
  isLoading = false;
  errorMessage = '';

  constructor(
    private examService: ExamService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadExams();
    this.timer=setInterval(()=>{this.updateStatuses();this.cdr.markForCheck();},1000);
  }

  ngOnDestroy() { if(this.timer) clearInterval(this.timer); }
  updateStatuses() {
    const now=Date.now()+this.clockOffset;
    for(const exam of this.exams) {
      const start=Date.parse(exam.startTime);
      exam.status=now<start ? 'upcoming' : now>=start+exam.durationMinutes*60000 ? 'completed' : 'active';
    }
  }
  formatStart(exam: ExamSummary) {
    return new Intl.DateTimeFormat('uk-UA',{timeZone:exam.timeZoneId ?? 'Europe/Chisinau',dateStyle:'short',timeStyle:'short'}).format(new Date(exam.startTime));
  }
  formatEnd(exam: ExamSummary) {
    return this.formatStart({...exam,startTime:new Date(Date.parse(exam.startTime)+exam.durationMinutes*60000).toISOString()});
  }
  loadExams() {
    this.isLoading = true;
    this.errorMessage = '';

    this.examService.getAll().subscribe({
      next: (data) => {
        this.clockOffset=data[0]?.serverTimeUtc ? Date.parse(data[0].serverTimeUtc)-Date.now() : 0;
        this.exams = (data || []).map((e) => ({
          id: e.id,
          title: e.title,
          description: e.description ?? '',
          startTime: e.scheduledStartTime ?? e.createdAt ?? '',
          timeZoneId: e.timeZoneId,
          durationMinutes: e.durationMinutes,
          questionsCount: e.questionCount ?? e.questions?.length ?? 0,
          status: e.scheduledStartTime && Date.parse(e.scheduledStartTime) > Date.now() ? 'upcoming' : 'active',
        }));
        this.updateStatuses();
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 401 || err.status === 403) {
          this.errorMessage = 'Для керування іспитами увійдіть у систему як викладач .';
        } else {
          this.errorMessage = 'Помилка завантаження іспитів: ' + (err.error?.message ?? err.message);
        }
        this.exams = [];
        this.cdr.detectChanges();
      },
    });
  }

  deleteExam(id: number) {
    if (confirm('Ви впевнені, що хочете видалити цей іспит?')) {
      this.examService.delete(id).subscribe({
        next: () => {
          this.exams = this.exams.filter(e => e.id !== id);
          this.cdr.detectChanges();
        },
        error: (err) => {
          alert('Помилка видалення іспиту: ' + (err.error?.message ?? err.message));
        }
      });
    }
  }

  copyLink(id: number) {
    // ID іспиту для введення в Qt Client
    const link = `ID Іспиту: ${id}`;
    navigator.clipboard.writeText(`${id}`);
    alert(`ID іспиту (#${id}) скопійовано!\nСтудент обирає цей іспит зі списку в клієнті.`);
  }
}