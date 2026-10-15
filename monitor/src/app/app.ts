import { Component, ChangeDetectorRef, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  private authSubscription?: Subscription;
  userLogin: string | null = null;
  isLoggedIn: boolean = false;

  constructor(
    public authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.authSubscription = this.authService.stateChanged.subscribe(() => { this.checkAuth(); this.cdr.markForCheck(); });
  }

  ngOnDestroy() { this.authSubscription?.unsubscribe(); }

  checkAuth() {
    this.isLoggedIn = this.authService.isAuthenticated();
    this.userLogin = this.authService.getLogin();
  }

  logout() {
    this.authService.logout();
    this.checkAuth();
    this.router.navigate(['/login']);
  }
}
