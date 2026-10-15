import { examinerGuard } from './auth/examiner.guard';
import { Routes } from '@angular/router';

// Users
import { StudentsComponent } from './pages/users/students/students';
import { AddStudentComponent } from './pages/users/add-student/add-student';
import { StudentDetailComponent } from './pages/users/student-detail/student-detail';
// Setup
import { ExamsComponent } from './pages/setup/exams/exams';
import { ExamBuilderComponent } from './pages/setup/exam-builder/exam-builder';

// Live
import { ActiveExamsHubComponent } from './pages/live/active-exams-hub/active-exams-hub';
import { MonitorComponent } from './pages/live/monitor/monitor';

// Archive
import { ReportsHubComponent } from './pages/archive/reports-hub/reports-hub';
import { ViolationDetailComponent } from './pages/archive/violation-detail/violation-detail';

import { AuthComponent } from './auth/auth.component';

export const routes: Routes = [
  { path: '', redirectTo: 'exams', pathMatch: 'full' },
  { path: 'login', component: AuthComponent, title: 'Вхід | Online Proctoring System' },
  
  // Users
  { path: 'students', component: StudentsComponent, canActivate: [examinerGuard], title: 'База студентів | Online Proctoring System' },
{ path: 'add-student', component: AddStudentComponent, canActivate: [examinerGuard], title: 'Додати студента | Online Proctoring System' },
  { path: 'student/:id', component: StudentDetailComponent, canActivate: [examinerGuard], title: 'Профіль студента | Online Proctoring System' },
  // Setup
  { path: 'exams', component: ExamsComponent, canActivate: [examinerGuard], title: 'Керування іспитами | Online Proctoring System' },
  { path: 'create-exam', component: ExamBuilderComponent, canActivate: [examinerGuard], title: 'Створення іспиту | Online Proctoring System' },
  
  // Live
  { path: 'monitoring', component: ActiveExamsHubComponent, canActivate: [examinerGuard], title: 'Активні іспити | Online Proctoring System' },
  { path: 'monitor/:id', component: MonitorComponent, canActivate: [examinerGuard], title: 'Live Моніторинг | Online Proctoring System' },
  
  // Archive
  { path: 'reports', component: ReportsHubComponent, canActivate: [examinerGuard], title: 'Звіти | Online Proctoring System' },
  { path: 'violation/:id', component: ViolationDetailComponent, canActivate: [examinerGuard], title: 'Деталі порушення | Online Proctoring System' },
  
  { path: '**', redirectTo: 'exams' }
];