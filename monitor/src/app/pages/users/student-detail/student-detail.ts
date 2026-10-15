import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { StudentService, StudentDto } from '../../../services/student.service';

@Component({
  selector: 'app-student-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './student-detail.html',
  styleUrl: './student-detail.css'
})
export class StudentDetailComponent implements OnInit, OnDestroy {
  student: StudentDto | null = null;
  photoUrl: string = '';
  isLoading = true;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private studentService: StudentService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/students']);
      return;
    }

    this.studentService.getById(id).subscribe({
      next: (data) => {
        this.student = data;
        this.studentService.getPhoto(id).subscribe({ next: blob => { this.photoUrl = URL.createObjectURL(blob); this.cdr.detectChanges(); }, error: () => {} });
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = 'Не вдалося завантажити дані студента: ' + (err.error?.message ?? err.message);
        this.cdr.detectChanges();
      }
    });
  }

  ngOnDestroy() { if (this.photoUrl) URL.revokeObjectURL(this.photoUrl); }

  deleteStudent = () => {
    if (!this.student) return;
    if (confirm(`Ви дійсно бажаєте видалити профіль студента "${this.student.fullName}"?`)) {
      this.studentService.delete(this.student.id).subscribe({
        next: () => {
          alert('Студента успішно видалено з бази');
          this.router.navigate(['/students']);
        },
        error: (err) => {
          alert('Помилка видалення студента: ' + (err.error?.message ?? err.message));
        }
      });
    }
  };
}