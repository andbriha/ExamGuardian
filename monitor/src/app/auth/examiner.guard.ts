import { map } from 'rxjs';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const examinerGuard: CanActivateFn = (_, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.ensureAuthenticated().pipe(map(signedIn => signedIn && auth.getRole() === 'Examiner'
    ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })));
};
