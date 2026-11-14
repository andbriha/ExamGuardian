import { Component, Input, Output, EventEmitter, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StudentService, StudentDto } from '../../../services/student.service';
import { ExamDto } from '../../../services/exam.service';
import { ExamSessionDto } from '../../../services/session.service';
import { groupResults, resultVerdict } from './group-results.model';
import { GroupReportDownloadComponent } from './group-report-download';

@Component({selector: 'app-group-results', standalone: true, imports: [CommonModule, FormsModule, RouterLink, GroupReportDownloadComponent], templateUrl: './group-results.html', styleUrl: './group-results.css'})
export class GroupResultsComponent {
  @Input() exams: ExamDto[] = [];
  @Input() sessions: ExamSessionDto[] = [];
  @Input() busy = false;
  @Output() review = new EventEmitter<string>();
  @Output() report = new EventEmitter<string>();
  students: StudentDto[] = [];
  group: string | null = null; examId = 0; search = ''; pendingOnly = false;
  loading = true; error = ''; verdict = resultVerdict;
  constructor(private service: StudentService, private cdr: ChangeDetectorRef) {}
  ngOnInit() { this.load(); }
  load() {
    this.loading = true; this.error = '';
    this.service.getAll().subscribe({next: students => {this.students = students; this.loading = false; this.cdr.markForCheck();}, error: () => {this.error = 'Не вдалося завантажити склад груп. Спробуйте ще раз.'; this.loading = false; this.cdr.markForCheck();}});
  }
  get groups() { return [...new Set(this.students.map(s => s.group || ''))].sort((a,b) => a.localeCompare(b)); }
  get availableExams() {
    const ids = new Set(this.students.filter(s => (s.group || '') === this.group).map(s => s.id));
    return this.exams.filter(e => !e.allowedGroup || e.allowedGroup === this.group || this.sessions.some(s => s.examId === e.id && ids.has(s.studentId)));
  }
  get rows() { return this.group === null ? [] : groupResults(this.students, this.sessions, this.group, this.examId); }
  get visibleRows() { return this.rows.filter(r => (!this.pendingOnly || (r.latest?.isFinished && r.latest.integrityVerdict === 'UnderReview')) && `${r.student.fullName} ${r.student.login}`.toLowerCase().includes(this.search.toLowerCase())); }
  get finished() { return this.rows.filter(r => r.latest?.isFinished).length; }
  get accepted() { return this.rows.filter(r => r.latest?.integrityVerdict === 'Accepted' && !r.latest.identityVerificationSkipped).length; }
  score(s?: ExamSessionDto) { return s?.isFinished && s.scoreCalculated && ['Completed', 'TimedOut'].includes(s.status) ? `${s.score}/100` : '—'; }
  selectGroup() { this.examId = 0; this.search = ''; this.pendingOnly = false; }
  download() {
    if (this.busy || !this.examId || !this.rows.length) return;
    const cell = (value: string) => '"' + (/^[\s]*[=+@-]/.test(value) || /[\t\r\n]/.test(value) ? "'" : '') + value.replace(/"/g, '""') + '"';
    const exam = this.exams.find(e => e.id === this.examId);
    const rows = [['Група','Іспит','ID іспиту','Студент','Логін','Бал','Висновок','Спроба'], ...this.rows.map(r => [this.group || 'Без групи',exam?.title || '',String(this.examId),r.student.fullName,r.student.login,this.score(r.latest),this.verdict(r.latest),String(r.latest?.id || '')])];
    const url = URL.createObjectURL(new Blob(['\uFEFF' + rows.map(r => r.map(cell).join(',')).join('\r\n')], {type:'text/csv;charset=utf-8'}));
    const a = document.createElement('a'); a.href = url; a.download = `group-results-${this.examId}.csv`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 30000);
  }
}
