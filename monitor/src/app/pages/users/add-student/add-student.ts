import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { StudentService } from '../../../services/student.service';

@Component({
  selector: 'app-add-student',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './add-student.html',
  styleUrl: './add-student.css'
})
export class AddStudentComponent implements OnInit {
  previewUrl: string | ArrayBuffer | null = null;
  selectedFile: File | null = null;
  
  availableGroups: string[] = ['КН-2024', 'ІПЗ-2023', 'КІ-2022', '121-ІПЗ', '122-КН'];
  
  newForm = {
    name: '',
    group: 'КН-2024',
    login: '',
    pass: ''
  };

  isSaving = false;
  copySuccess = false;
  errorMessage = '';

  constructor(
    private router: Router,
    private cdr: ChangeDetectorRef,
    private studentService: StudentService
  ) {}

  ngOnInit() {
    this.generateCredentials();
  }

  // Українська транслітерація (паспортний стандарт КМУ)
  transliterate(text: string): string {
    const map: Record<string, string> = {
      'а': 'a', 'б': 'b', 'в': 'v', 'г': 'h', 'ґ': 'g', 'д': 'd', 'е': 'e', 'є': 'ye', 'ж': 'zh',
      'з': 'z', 'и': 'y', 'і': 'i', 'ї': 'yi', 'й': 'y', 'к': 'k', 'л': 'l', 'м': 'm', 'н': 'n',
      'о': 'o', 'п': 'p', 'р': 'r', 'с': 's', 'т': 't', 'у': 'u', 'ф': 'f', 'х': 'kh', 'ц': 'ts',
      'ч': 'ch', 'ш': 'sh', 'щ': 'shch', 'ю': 'yu', 'я': 'ya', 'ь': '', 'ъ': '', 'э': 'e', 'ы': 'y'
    };
    return text.toLowerCase().split('').map(c => map[c] !== undefined ? map[c] : c).join('').replace(/[^a-z0-9]/g, '');
  }

  onNameChange() {
    if (!this.newForm.name.trim()) return;
    this.generateCredentials();
  }

  generateCredentials() {
    const cleanName = this.newForm.name.trim();
    let loginBase = '';

    if (cleanName) {
      const parts = cleanName.split(/\s+/).filter(Boolean);
      if (parts.length >= 2) {
        // e.g. "Андрій Коваленко" -> a.kovalenko
        const first = this.transliterate(parts[0]);
        const last = this.transliterate(parts[parts.length - 1]);
        loginBase = `${first[0] || 's'}.${last}`;
      } else {
        loginBase = this.transliterate(parts[0]);
      }
    }

    if (!loginBase) {
      loginBase = 'student';
    }

    // Add 2-digit unique suffix for collision resistance
    const randNum = Math.floor(10 + Math.random() * 90);
    this.newForm.login = `${loginBase}_${randNum}`;

    // Generate secure and readable password
    const words = ['Guard', 'Shield', 'Study', 'Exam', 'Cyber', 'Smart', 'Eagle', 'Falcon'];
    const word = words[Math.floor(Math.random() * words.length)];
    const passNum = Math.floor(1000 + Math.random() * 9000);
    const symbols = ['#', '!', '$', '%', '@'];
    const sym = symbols[Math.floor(Math.random() * symbols.length)];
    this.newForm.pass = `${word}-${passNum}${sym}`;

    this.copySuccess = false;
    this.cdr.detectChanges();
  }

  selectGroup(grp: string) {
    this.newForm.group = grp;
    this.cdr.detectChanges();
  }

  copyCredentials() {
    const text = `Студент: ${this.newForm.name || 'Студент'}\nГрупа: ${this.newForm.group}\nЛогін: ${this.newForm.login}\nПароль: ${this.newForm.pass}`;
    navigator.clipboard.writeText(text).then(() => {
      this.copySuccess = true;
      setTimeout(() => {
        this.copySuccess = false;
        this.cdr.detectChanges();
      }, 3000);
      this.cdr.detectChanges();
    }).catch(() => {
      alert(`Облікові дані:\n${text}`);
    });
  }

  onFile = (e: any) => {
    const file = e.target.files[0];
    if (!file) return;
    this.selectedFile = file;

    const reader = new FileReader();
    reader.onload = () => {
      this.previewUrl = reader.result;
      this.cdr.detectChanges(); 
    };
    reader.readAsDataURL(file);
  };

  save = () => {
    if (!this.newForm.name || !this.newForm.login || !this.newForm.pass) {
      alert('Будь ласка, заповніть усі обов\'язкові поля');
      return;
    }

    this.isSaving = true;
    this.studentService.create({
      fullName: this.newForm.name.trim(),
      login: this.newForm.login.trim(),
      password: this.newForm.pass.trim(),
      group: this.newForm.group.trim() || 'Основна група',
      role: 'Student'
    }).subscribe({
      next: (created) => {
        if (this.selectedFile && created?.id) {
          this.studentService.uploadPhoto(created.id, this.selectedFile).subscribe({
            next: () => {
              alert(`Студента ${this.newForm.name} (${this.newForm.group}) та AI-фото успішно додано!`);
              this.router.navigate(['/students']);
            },
            error: () => {
              alert('Студента додано, але виникла помилка під час збереження фото');
              this.router.navigate(['/students']);
            }
          });
        } else {
          alert(`Студента ${this.newForm.name} (${this.newForm.group}) успішно додано!`);
          this.router.navigate(['/students']);
        }
      },
      error: (err) => {
        this.isSaving = false;
        if (err.status === 401 || err.status === 403) {
          if (confirm('Для додавання студентів потрібні права викладача (Ви не авторизовані).\n\nПерейти на сторінку входу? (Логін: admin, Пароль: password12345)')) {
            this.router.navigate(['/login']);
          }
        } else {
          alert('Помилка додавання студента: ' + (err.error?.message ?? err.message));
        }
      }
    });
  };
}