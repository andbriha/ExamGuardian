import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-students-component',
  imports: [CommonModule, FormsModule],
  templateUrl: './students-component.html',
  styleUrl: './students-component.css',
})
export class StudentsComponent {
searchQuery: string = '';

  // Заглушка даних (потім буде запит до ASP.NET)
  students = [
    { id: 1, name: 'Іванов Іван', group: 'КН-401', login: 'ivanov_4321', status: 'online', lastExam: '15.12.2025' },
    { id: 2, name: 'Петренко Марія', group: 'КН-401', login: 'petrenko_9912', status: 'offline', lastExam: '14.12.2025' },
    { id: 3, name: 'Сидорчук Олег', group: 'КН-302', login: 'sydor_7765', status: 'offline', lastExam: '10.12.2025' }
  ];

  filteredStudents() {
    return this.students.filter(s => 
      s.name.toLowerCase().includes(this.searchQuery.toLowerCase()) || 
      s.group.toLowerCase().includes(this.searchQuery.toLowerCase())
    );
  }
}
