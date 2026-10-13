import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  userLogin: string | null = null;
  isLoggedIn: boolean = false;

  constructor(
    public authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.checkAuth();
  }

  checkAuth() {
    this.isLoggedIn = this.authService.isAuthenticated();
    this.userLogin = this.authService.getLogin() || (this.isLoggedIn ? 'admin' : null);
  }

  logout() {
    this.authService.logout();
    this.checkAuth();
    this.router.navigate(['/login']);
  }
}
