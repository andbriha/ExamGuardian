import { Routes } from '@angular/router';
// Імпортуємо ваші компоненти (замініть шляхи на реальні, де лежать ваші файли)
import { InfoComponent } from './pages/info/info-component';
import { ExamsComponent } from './pages/exams/exams-component';
import { StudentsComponent } from './pages/students/students-component';

export const routes: Routes = [

  { path: '', redirectTo: 'info', pathMatch: 'full' },
  { 
    path: 'info', 
    component: InfoComponent,
    title: 'Інфо & Допомога | ExamGuardian' 
  },
  { 
  path: 'exams', 
  component: ExamsComponent, 
  title: 'Керування іспитами | ExamGuardian' 
},
  { 
  path: 'students', 
  component: StudentsComponent, 
  title: 'Керування іспитами | ExamGuardian' 
},
  { path: '**', redirectTo: 'info' }
];