import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
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
export class ActiveExamsHubComponent implements OnInit {
  activeExams: ActiveExamView[] = [];
  isLoading = true;
  errorMessage = '';

  constructor(
    private examService: ExamService,
    private sessionService: SessionService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
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
    this.activeExams = exams.map(e => {
      const examSessions = sessions.filter(s => s.examId === e.id);
      const activeCount = examSessions.filter(s => !s.isFinished).length;
      const totalCount = examSessions.length > 0 ? examSessions.length : 1;

      return {
        id: e.id.toString(),
        title: e.title,
        groupName: e.description || 'Загальна група',
        activeStudents: activeCount,
        totalStudents: totalCount,
        startTime: `${e.durationMinutes} хв. тривалість`,
        durationMinutes: e.durationMinutes
      };
    });
  }
}