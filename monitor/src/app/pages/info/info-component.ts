import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-info-component',
  imports: [CommonModule, FormsModule,FormsModule],
  templateUrl: './info-component.html',
  styleUrl: './info-component.css',
})
export class InfoComponent {
studentData = {
    name: '',
    group: '',
    password: '',
    features: ''
  };

  generatedLogin = 'student_---';
  previewImage: string | null = null;

  constructor() {
    this.generateCredentials();
  }

  generateCredentials() {
    // Генерація логіна
    const randomId = Math.floor(1000 + Math.random() * 9000);
    if (this.studentData.name) {
      const translit = this.studentData.name.split(' ')[0].toLowerCase();
      this.generatedLogin = `${translit}_${randomId}`;
    } else {
      this.generatedLogin = `user_${randomId}`;
    }

    // Генерація пароля (8 символів)
    this.studentData.password = Math.random().toString(36).slice(-8);
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e: any) => this.previewImage = e.target.result;
      reader.readAsDataURL(file);
    }
  }

  saveStudent() {
    // Тут буде запит до ASP.NET
    console.log('Відправка на сервер:', {
      ...this.studentData,
      login: this.generatedLogin,
      image: this.previewImage
    });
    alert('Студент успішно доданий до бази!');
  }
}