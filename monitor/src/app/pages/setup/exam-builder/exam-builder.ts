// exam-builder.component.ts
import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GroupService } from '../../../services/group.service';
import { RouterLink } from '@angular/router';
import { ExamService, QtiPreview } from '../../../services/exam.service';

import { DraftQuestion as Question, QuestionKind, createQuestion, importedQuestion } from './question-types';

interface CreateExamDto {
  title: string;
  description: string;
  startTime: string;
  durationMinutes: number;
  creatorId: number;
  questions: {
    text: string;
    points: number;
    correctIndex: number;
    orderIndex: number;
    options: string[];
  }[];
}

@Component({
  selector: 'app-exam-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './exam-builder.html',
  styleUrl: './exam-builder.css',
})
export class ExamBuilderComponent implements OnInit {
  qtiPreview: QtiPreview | null = null;
  importing = false;
  importError = '';
  importQti(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0]; input.value = '';
    this.qtiPreview = null; this.importError = '';
    if (!file) return;
    if (file.size > 10 * 1024 * 1024) { this.importError = 'Ліміт файлу — 10 МБ.'; return; }
    this.importing = true;
    this.examService.importQti(file).subscribe({
      next: preview => { this.qtiPreview = preview; this.importing = false; this.cdr.markForCheck(); },
      error: error => { this.importError = error.error?.message || 'Не вдалося прочитати файл.'; this.importing = false; this.cdr.markForCheck(); }
    });
  }
  applyImport() {
    if (!this.qtiPreview?.canImport) return;
    const existing = this.questions.filter(q => q.text.trim() || q.options.some(o => o.text.trim()));
    if (existing.length + this.qtiPreview.questions.length > 500) { this.importError = 'Максимум 500 запитань в іспиті.'; return; }
    this.questions = [...existing, ...this.qtiPreview.questions.map(importedQuestion)];
    this.qtiPreview = null;
    this.successMsg = 'Запитання додано до чернетки. Перевірте відповіді, бали та розклад перед створенням іспиту.';
  }
  groups: string[] = [];
  groupsError = false;
  ngOnInit() { this.groupService.getAll().subscribe({next: groups => { this.groups = groups; this.cdr.markForCheck(); }, error: () => { this.groupsError = true; this.cdr.markForCheck(); }}); }
  exam = {
    title: '',
    description: '',
    startTime: '',
    timeZoneId: 'Europe/Chisinau',
    durationMinutes: 60,
    allowedGroup: '', maxAttempts: 1, passingScore: 60, allowSpeech: false, flagHeadMovement: true, allowBook: false, allowLaptop: false,
  };

  questions: Question[] = [];
  saving = false;
  errorMsg = '';
  successMsg = '';

  constructor(private groupService: GroupService, private examService: ExamService, private cdr: ChangeDetectorRef) {
    this.addQuestion(); // починаємо з одного питання
  }

  addQuestion(kind: QuestionKind = 'singleChoice') {
    this.questions.push(createQuestion(kind));
  }

  removeQuestion(index: number) {
    if (this.questions.length > 1)
      this.questions.splice(index, 1);
  }

  addOption(q: Question) {
    if (q.kind !== 'trueFalse' && q.options.length < 10)
      q.options.push({ text: '' });
  }

  removeOption(q: Question, i: number) {
    if (q.kind !== 'trueFalse' && q.options.length > 2) {
      q.options.splice(i, 1);
      if (i < q.correctIndex) q.correctIndex--;
      if (q.correctIndex >= q.options.length)
        q.correctIndex = q.options.length - 1;
    }
  }

  isValid(): boolean {
    if (!this.exam.title.trim() || !this.exam.startTime || this.exam.durationMinutes < 1 || this.exam.durationMinutes > 480 || this.exam.maxAttempts < 1 || this.exam.maxAttempts > 10 || this.exam.passingScore < 0 || this.exam.passingScore > 100) return false;
    return this.questions.every(q =>
      q.text.trim() &&
      q.options.length >= 2 &&
      q.options.every(o => o.text.trim()) &&
      q.correctIndex < q.options.length
    );
  }

  getTotalPoints(): number {
    return this.questions.reduce((sum, q) => sum + q.points, 0);
  }

  createExam() {
    if (!this.isValid()) {
      this.errorMsg = 'Заповніть усі поля та варіанти відповідей';
      return;
    }

    this.saving = true;
    this.errorMsg = '';

    const payload = {
      title:           this.exam.title,
      description:     this.exam.description,
      scheduledStartLocal: this.exam.startTime || undefined,
        timeZoneId: this.exam.timeZoneId,
      durationMinutes: this.exam.durationMinutes,
      allowedGroup: this.exam.allowedGroup.trim() || undefined, maxAttempts: this.exam.maxAttempts, passingScore: this.exam.passingScore,
      allowSpeech: this.exam.allowSpeech, flagHeadMovement: this.exam.flagHeadMovement, allowedObjects: [...(this.exam.allowBook ? ["book"] : []), ...(this.exam.allowLaptop ? ["laptop"] : [])],
      questions:       this.questions.map((q, i) => ({
        text:         q.text,
        points:       q.points,
        correctIndex: q.correctIndex,
        orderIndex:   i,
        options:      q.options.map(o => o.text),
      })),
    };

    this.examService.create(payload)
      .subscribe({
        next: () => {
          this.successMsg = 'Іспит успішно створено';
          this.saving = false; this.cdr.markForCheck();
        },
        error: (err) => {
          this.errorMsg = err.error?.message ?? 'Помилка сервера';
          this.saving = false; this.cdr.markForCheck();
        },
      });
  }
}