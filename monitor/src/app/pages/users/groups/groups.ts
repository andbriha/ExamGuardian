import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { GroupService } from '../../../services/group.service';
import { StudentService, StudentDto } from '../../../services/student.service';

@Component({
  selector: 'app-groups', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './groups.html', styleUrl: './groups.css'
})
export class GroupsComponent implements OnInit {
  groups: string[] = [];
  students: StudentDto[] = [];
  name = ''; selected = ''; studentId = '';
  loading = true; saving = false; assigning = false;
  error = ''; success = '';
  constructor(private service: GroupService, private studentService: StudentService, private cdr: ChangeDetectorRef) {}
  ngOnInit() { this.load(); }
  load() {
    this.loading = true; this.error = '';
    forkJoin({groups: this.service.getAll(), students: this.studentService.getAll()}).subscribe({
      next: ({groups, students}) => {
        this.groups = groups; this.students = students;
        if (!groups.includes(this.selected)) this.selected = groups[0] || '';
        this.loading = false; this.cdr.markForCheck();
      },
      error: () => { this.error = 'Не вдалося завантажити групи. Спробуйте ще раз.'; this.loading = false; this.cdr.markForCheck(); }
    });
  }
  get members() { return this.students.filter(s => s.group === this.selected); }
  get candidates() { return this.students.filter(s => s.group !== this.selected); }
  count(name: string) { return this.students.filter(s => s.group === name).length; }
  select(name: string) { this.selected = name; this.studentId = ''; this.success = ''; }
  create() {
    if (!this.name.trim() || this.saving) return;
    this.saving = true; this.error = ''; this.success = '';
    this.service.create(this.name.trim()).subscribe({
      next: ({name}) => {
        this.groups = [...this.groups, name].sort((a,b) => a.localeCompare(b));
        this.select(name); this.name = ''; this.saving = false;
        this.success = 'Групу створено. Тепер додайте студентів.'; this.cdr.markForCheck();
      },
      error: err => { this.error = err.error?.message || 'Не вдалося створити групу.'; this.saving = false; this.cdr.markForCheck(); }
    });
  }
  assign() {
    if (!this.studentId || !this.selected || this.assigning) return;
    const id = this.studentId, group = this.selected;
    this.assigning = true; this.error = ''; this.success = '';
    this.service.assign(id, group).subscribe({
      next: () => {
        this.students = this.students.map(s => s.id === id ? {...s, group} : s);
        this.studentId = ''; this.assigning = false; this.success = 'Групу студента оновлено.'; this.cdr.markForCheck();
      },
      error: err => { this.error = err.error?.message || 'Не вдалося змінити групу.'; this.assigning = false; this.cdr.markForCheck(); }
    });
  }
}
