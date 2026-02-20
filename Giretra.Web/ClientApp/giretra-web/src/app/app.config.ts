import {
  APP_INITIALIZER,
  ApplicationConfig,
  InjectionToken,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHotToastConfig } from '@ngxpert/hot-toast';

import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { AuthService } from './core/services/auth.service';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import {
  LUCIDE_ICONS,
  LucideIconProvider,
  LogOut,
  Layers,
  Bot,
  UserPlus,
  Plus,
  LogIn,
  Eye,
  Users,
  Scissors,
  Trophy,
  Check,
  ChevronsUp,
  X,
  Lock,
  Unlock,
  Link,
  UserX,
  Settings,
  Search,
  UserMinus,
  Ban,
  ChevronLeft,
  ChevronRight,
  Pencil,
  Upload,
  Trash2,
  EyeOff,
  Shield,
  Flame,
  Calendar,
  Star,
} from 'lucide-angular';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

const usedIcons = {
  LogOut,
  Layers,
  Bot,
  UserPlus,
  Plus,
  LogIn,
  Eye,
  Users,
  Scissors,
  Trophy,
  Check,
  ChevronsUp,
  X,
  Lock,
  Unlock,
  Link,
  UserX,
  Settings,
  Search,
  UserMinus,
  Ban,
  ChevronLeft,
  ChevronRight,
  Pencil,
  Upload,
  Trash2,
  EyeOff,
  Shield,
  Flame,
  Calendar,
  Star,
};

function initializeKeycloak(auth: AuthService) {
  return () => auth.init();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeKeycloak,
      deps: [AuthService],
      multi: true,
    },
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: LUCIDE_ICONS, multi: true, useValue: new LucideIconProvider(usedIcons) },
    provideHotToastConfig({
      position: 'bottom-center',
      dismissible: true,
      autoClose: true,
      theme: 'snackbar',
      style: {
        background: 'hsl(220 20% 14%)',
        color: 'hsl(210 40% 96%)',
        border: '1px solid hsl(220 15% 25%)',
      },
    }),
  ],
};
