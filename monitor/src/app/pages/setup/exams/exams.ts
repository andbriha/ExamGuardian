import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { ExamService, ExamDto } from '../../../services/exam.service';

export interface ExamSummary {
  id: number;
  title: string;
  description: string;
  startTime: string;
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
export class ExamsComponent implements OnInit {
  exams: ExamSummary[] = [];
  isLoading = false;
  errorMessage = '';

  constructor(
    private examService: ExamService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadExams();
  }

  loadExams() {
    this.isLoading = true;
    this.errorMessage = '';

    this.examService.getAll().subscribe({
      next: (data) => {
        this.exams = (data || []).map((e) => ({
          id: e.id,
          title: e.title,
          description: e.description ?? '',
          startTime: e.scheduledStartTime ?? new Date().toISOString(),
          durationMinutes: e.durationMinutes,
          questionsCount: e.questions?.length ?? 0,
          status: 'active',
        }));
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 401 || err.status === 403) {
          this.errorMessage = 'Для керування іспитами увійдіть у систему як викладач (admin / password12345).';
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
    alert(`ID іспиту (#${id}) скопійовано!\nСтудент може ввести цей ID у Qt Client або перейти за посиланням.`);
  }
}