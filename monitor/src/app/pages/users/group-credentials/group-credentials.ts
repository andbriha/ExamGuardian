import { Component, Input, ChangeDetectorRef, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { StudentDto } from '../../../services/student.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-group-credentials', standalone: true, imports: [CommonModule],
  template: `
    <section aria-label="Доступи групи">
      <h4>Доступи студентів</h4>
      <p>PDF містить доступи всієї групи для викладача. Завантаження не змінює паролі. Якщо пароль раніше не зберігався або змінений, у документі буде позначка «Не збережено».</p>
      <button (click)="download(false, 'pdf')" [disabled]="busy || !members.length">Скачати доступи групи PDF</button>
      <button (click)="download(false)" [disabled]="busy || !members.length">Завантажити логіни CSV</button>
      <button (click)="prepare()" [disabled]="busy || !members.length">Випустити паролі…</button>
      @if (confirmation && confirmedGroup === group) {
        <div class="confirmation">
          <strong>Нові паролі для {{ confirmed.length }} студентів групи «{{ group }}»</strong>
          <p>Попередні паролі перестануть працювати. Видані системою паролі зберігаються зашифровано та доступні повторно у PDF. Збережіть його та передайте кожному студенту тільки його власні дані.</p>
          <button (click)="download(true, 'pdf')" [disabled]="busy">{{ busy ? 'Формування…' : 'Замінити паролі та завантажити PDF' }}</button>
          <button (click)="confirmation = false" [disabled]="busy">Скасувати</button>
        </div>
      }
      @if (error) { <p role="alert">{{ error }}</p> }
      @if (result) {
        <p role="status">{{ message }} <a [href]="result" [download]="filename">Завантажити файл повторно</a></p>
      }
    </section>`,
  styles: [`section{padding:16px;background:#f0fdfa;border:1px solid #99f6e4;border-radius:10px;margin:16px 0}button{padding:10px;margin:4px;border:1px solid #0f766e;border-radius:6px;background:white;color:#115e59;cursor:pointer}button:disabled{opacity:.5;cursor:wait}.confirmation{padding:14px;margin-top:12px;background:#fffbeb;border:1px solid #fcd34d;border-radius:8px}p{line-height:1.5}a{color:#0f766e}`]
})
export class GroupCredentialsComponent {
  @Input() group = '';
  @Input() members: StudentDto[] = [];
  busy = false; confirmation = false; confirmedGroup = ''; confirmed: string[] = [];
  error = ''; message = ''; result = ''; filename = '';
  private epoch = 0;
  ngOnChanges(changes: SimpleChanges) {
    if (!changes['group']) return;
    this.epoch++; this.busy=false; this.confirmation=false; this.error=''; this.message='';
    if (this.result) URL.revokeObjectURL(this.result);
    this.result='';
  }
  constructor(private http: HttpClient, private cdr: ChangeDetectorRef) {}
  prepare() { this.confirmedGroup = this.group; this.confirmed = this.members.map(s => s.id); this.confirmation = true; this.error = ''; }
  ngOnDestroy() { if (this.result) URL.revokeObjectURL(this.result); }
  download(reset: boolean, format: 'csv' | 'pdf' = 'csv') {
    if (this.busy || !this.members.length || (reset && (!this.confirmation || this.confirmedGroup !== this.group))) return;
    const group = this.group;
    const epoch = this.epoch;
    this.busy = true; this.error = '';
    const url = `${environment.apiUrl}/groups/credentials?format=${format}`;
    const request = reset ? this.http.post(url, {group, studentIds: this.confirmed}, {responseType: 'blob'}) : this.http.get(url, {params: {group}, responseType: 'blob'});
    request.subscribe({next: blob => {
      if (epoch !== this.epoch) return;
      if (this.result) URL.revokeObjectURL(this.result);
      this.result = URL.createObjectURL(blob); this.filename = `${reset || format === 'pdf' ? 'credentials' : 'logins'}-${group}.${format}`;
      const a = document.createElement('a'); a.href = this.result; a.download = this.filename; a.click();
      this.message = reset ? `Нові паролі групи «${group}» збережено. Якщо завантаження не почалося, скористайтеся посиланням.` : `Документ групи «${group}» готовий. Паролі не змінено.`;
      this.busy = false; this.confirmation = false; this.cdr.markForCheck();
    }, error: async err => {
      if (epoch !== this.epoch) return;
      let message = reset ? 'Не отримано підтвердження. Спочатку завантажте доступи без перевипуску: паролі могли вже змінитися.' : 'Не вдалося сформувати файл. Перевірте з’єднання та повторіть завантаження.';
      try { message = JSON.parse(await err.error.text()).message || message; } catch {}
      if (epoch !== this.epoch) return;
      this.error = message;
      this.busy = false; this.cdr.markForCheck();
    }});
  }
}
