import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Only intercept requests to our API
  const isApiRequest = environment.apiBaseUrl
    ? req.url.startsWith(environment.apiBaseUrl)
    : !req.url.startsWith('http');
  if (!isApiRequest) {
    return next(req);
  }

  const auth = inject(AuthService);

  return from(auth.getToken()).pipe(
    switchMap((token) => {
      const scheme = auth.offlineMode() ? 'Offline' : 'Bearer';
      const authReq = req.clone({
        setHeaders: { Authorization: `${scheme} ${token}` },
      });
      return next(authReq);
    }),
  );
};
