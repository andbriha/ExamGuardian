import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

import { StudentService } from '../../../services/student.service';

export interface StudentView {
  id: string;
  fullName: string;
  groupName: string;
  login: string;
  hasPhoto: boolean;
  isActive: boolean;
}

@Component({
  selector: 'app-students',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './students.html',
  styleUrl: './students.css'
})
export class StudentsComponent implements OnInit {
  students: StudentView[] = [];
  groups: string[] = ['Всі групи'];
  selectedGroup: string = 'Всі групи';
  searchText = '';
  viewMode: 'list' | 'grouped' = 'grouped';
  isLoading = true;
  errorMessage = '';

  constructor(
    private studentService: StudentService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadStudents();
  }

  loadStudents() {
    this.isLoading = true;
    this.studentService.getAll().subscribe({
      next: (data) => {
        this.isLoading = false;
        this.students = (data || []).map((s) => ({
          id: s.id,
          fullName: s.fullName,
          groupName: s.group || 'Основна група',
          login: s.login,
          hasPhoto: !!s.photoUrl,
          isActive: s.isActive ?? true,
        }));

        // Extract unique groups
        const uniqueGroups = Array.from(new Set(this.students.map(s => s.groupName))).filter(Boolean);
        this.groups = ['Всі групи', ...uniqueGroups.sort()];

        this.cdr.detectChanges();
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 401 || err.status === 403) {
          this.errorMessage = 'Для перегляду бази студентів потрібно авторизуватися як викладач (admin / password12345).';
        } else {
          this.errorMessage = 'Помилка завантаження списку студентів: ' + (err.error?.message ?? err.message);
        }
        this.students = [];
        this.cdr.detectChanges();
      },
    });
  }

  get filteredStudents() {
    return this.students.filter(s => {
      const matchSearch = s.fullName.toLowerCase().includes(this.searchText.toLowerCase()) || 
                          s.login.toLowerCase().includes(this.searchText.toLowerCase()) ||
                          s.groupName.toLowerCase().includes(this.searchText.toLowerCase());
      const matchGroup = this.selectedGroup === 'Всі групи' || s.groupName === this.selectedGroup;
      return matchSearch && matchGroup;
    });
  }

  get activeGroups(): string[] {
    if (this.selectedGroup !== 'Всі групи') {
      return [this.selectedGroup];
    }
    return this.groups.filter(g => g !== 'Всі групи');
  }

  getStudentsInGroup(groupName: string): StudentView[] {
    return this.filteredStudents.filter(s => s.groupName === groupName);
  }

  selectGroup(grp: string) {
    this.selectedGroup = grp;
    this.cdr.detectChanges();
  }

  toggleStatus = (s: StudentView) => s.isActive = !s.isActive;
}