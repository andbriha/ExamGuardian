import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ExamService, ExamDto } from '../../../services/exam.service';
import { SessionService, ExamSessionDto } from '../../../services/session.service';

export interface ActiveExamView {
  id: string;
  title: string;
  groupName: string;
  activeStudents: number;
  totalStudents: number;
  startTime: string;
  durationMinutes: number;
}

@Component({
  selector: 'app-active-exams-hub',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './active-exams-hub.html',
  styleUrl: './active-exams-hub.css'
})
export class ActiveExamsHubComponent implements OnInit, OnDestroy {
  activeExams: ActiveExamView[] = [];
  private timer?: ReturnType<typeof setInterval>;
  isLoading = true;
  errorMessage = '';

  constructor(
    private examService: ExamService,
    private sessionService: SessionService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadData();
    this.timer=setInterval(()=>this.loadData(),10000);
  }

  ngOnDestroy() { if(this.timer)clearInterval(this.timer); }

  loadData() {
    this.errorMessage = '';
    this.isLoading = this.activeExams.length === 0;
    this.examService.getAll().subscribe({
      next: (exams) => {
        this.sessionService.getAll().subscribe({
          next: (sessions) => {
            this.buildExamViews(exams, sessions);
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            // If sessions fail or none exist, still show exams
            this.buildExamViews(exams, []);
            this.isLoading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = 'Не вдалося завантажити перелік іспитів: ' + (err.error?.message ?? err.message);
        this.cdr.detectChanges();
      }
    });
  }

  private buildExamViews(exams: ExamDto[], sessions: ExamSessionDto[]) {
    this.activeExams = exams.filter(e => {
      const now=e.serverTimeUtc ? Date.parse(e.serverTimeUtc) : Date.now();
      const start=Date.parse(e.scheduledStartTime ?? e.createdAt ?? '');
      return now>=start && now<start+e.durationMinutes*60000;
    }).map(e => {
      const examSessions = sessions.filter(s => s.examId === e.id);
      const activeCount = examSessions.filter(s => !s.isFinished).length;
      const totalCount = examSessions.length;

      return {
        id: e.id.toString(),
        title: e.title,
        groupName: e.allowedGroup || 'Усі групи',
        activeStudents: activeCount,
        totalStudents: totalCount,
        startTime: `${e.durationMinutes} хв. тривалість`,
        durationMinutes: e.durationMinutes
      };
    });
  }
}