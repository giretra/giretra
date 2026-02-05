import { ApplicationConfig, InjectionToken, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';
import { environment } from '../environments/environment';
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
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: LUCIDE_ICONS, multi: true, useValue: new LucideIconProvider(usedIcons) },
  ],
};
