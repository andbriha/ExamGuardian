import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-exams-component',
  imports: [CommonModule, FormsModule,FormsModule],
  templateUrl: './exams-component.html',
  styleUrl: './exams-component.css',
})
export class ExamsComponent {
exam = {
    title: '',
    startTime: '',
    duration: 60,
    testData: null
  };
  fileName: string = '';

  onTestUpload(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.fileName = file.name;
      const reader = new FileReader();
      reader.onload = (e: any) => {
        // Парсимо JSON для перевірки перед відправкою
        this.exam.testData = JSON.parse(e.target.result);
      };
      reader.readAsText(file);
    }
  }

  createExam() {
    console.log('Створення іспиту в ASP.NET:', this.exam);
    // Виклик сервісу: this.examService.create(this.exam).subscribe(...)
    alert('Іспит успішно заплановано!');
  }
}
