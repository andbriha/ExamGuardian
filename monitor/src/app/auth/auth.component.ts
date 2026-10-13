import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.css'
})
export class AuthComponent {
  login = '';
  password = '';
  isLoading = false;
  errorMessage = '';

  constructor(private authService: AuthService, private router: Router) {}

  onLoginSubmit() {
    if (!this.login || !this.password) return;

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.login, this.password).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/exams']);
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.error?.message ?? 'Невірний логін або пароль';
      }
    });
  }
}
