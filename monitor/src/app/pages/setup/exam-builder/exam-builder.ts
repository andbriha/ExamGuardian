// exam-builder.component.ts
import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExamService } from '../../../services/exam.service';

interface QuestionOption {
  text: string;
}

interface Question {
  text: string;
  points: number;
  correctIndex: number;
  options: QuestionOption[];
}

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
  imports: [CommonModule, FormsModule],
  templateUrl: './exam-builder.html',
  styleUrl: './exam-builder.css',
})
export class ExamBuilderComponent {
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

  constructor(private examService: ExamService, private cdr: ChangeDetectorRef) {
    this.addQuestion(); // починаємо з одного питання
  }

  addQuestion() {
    this.questions.push({
      text: '',
      points: 1,
      correctIndex: 0,
      options: [{ text: '' }, { text: '' }, { text: '' }, { text: '' }],
    });
  }

  removeQuestion(index: number) {
    if (this.questions.length > 1)
      this.questions.splice(index, 1);
  }

  addOption(q: Question) {
    if (q.options.length < 6)
      q.options.push({ text: '' });
  }

  removeOption(q: Question, i: number) {
    if (q.options.length > 2) {
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