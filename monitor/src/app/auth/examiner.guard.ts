import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const examinerGuard: CanActivateFn = (_, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() && localStorage.getItem('user_role') === 'Examiner'
    ? true : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
