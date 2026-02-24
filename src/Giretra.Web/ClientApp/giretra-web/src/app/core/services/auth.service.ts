import { Injectable, inject, signal, computed } from '@angular/core';
import Keycloak from 'keycloak-js';
import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';

export interface AuthUser {
  keycloakId: string;
  username: string;
  displayName: string;
  email: string | undefined;
  roles: string[];
}

const OFFLINE_USERNAME_KEY = 'giretra-offline-username';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly api = inject(ApiService);
  private keycloak: Keycloak | null = null;
  private refreshInterval: ReturnType<typeof setInterval> | null = null;
  private offlineUsername: string | null = null;

  private readonly _authenticated = signal(false);
  private readonly _user = signal<AuthUser | null>(null);
  private readonly _offlineMode = signal(false);

  readonly authenticated = this._authenticated.asReadonly();
  readonly user = this._user.asReadonly();
  readonly hasUser = computed(() => !!this._user());
  readonly offlineMode = this._offlineMode.asReadonly();

  async init(): Promise<void> {
    // Check auth mode from server
    const authMode = await this.fetchAuthConfig();

    if (authMode === 'offline') {
      await this.initOffline();
      return;
    }

    await this.initKeycloak();
  }

  async getToken(): Promise<string> {
    if (this._offlineMode()) {
      return this.offlineUsername ?? '';
    }

    if (!this.keycloak) {
      throw new Error('Keycloak not initialized');
    }

    // Refresh if token expires within 30 seconds
    try {
      await this.keycloak.updateToken(30);
    } catch {
      // Token refresh failed, redirect to login
      await this.keycloak.login();
    }

    return this.keycloak.token!;
  }

  logout(): void {
    if (this._offlineMode()) {
      localStorage.removeItem(OFFLINE_USERNAME_KEY);
      window.location.reload();
      return;
    }

    this.stopTokenRefresh();
    this.keycloak?.logout({ redirectUri: window.location.origin });
  }

  updateLocalDisplayName(newName: string): void {
    const current = this._user();
    if (current) this._user.set({ ...current, displayName: newName });
  }

  private async fetchAuthConfig(): Promise<string> {
    try {
      const baseUrl = environment.apiBaseUrl || '';
      const res = await fetch(`${baseUrl}/api/auth/config`);
      if (res.ok) {
        const data = await res.json();
        return data.mode ?? 'keycloak';
      }
    } catch {
      console.warn('[Auth] Failed to fetch auth config, defaulting to keycloak');
    }
    return 'keycloak';
  }

  private async initOffline(): Promise<void> {
    this._offlineMode.set(true);

    // Try stored username first
    let username = localStorage.getItem(OFFLINE_USERNAME_KEY);

    if (!username) {
      username = window.prompt('Enter your username:');
      if (!username) {
        username = 'Player';
      }
      localStorage.setItem(OFFLINE_USERNAME_KEY, username);
    }

    this.offlineUsername = username;
    this._authenticated.set(true);
    this._user.set({
      keycloakId: '',
      username: username,
      displayName: username,
      email: `${username}@offline`,
      roles: [],
    });
  }

  private async initKeycloak(): Promise<void> {
    this.keycloak = new Keycloak({
      url: environment.keycloak.url,
      realm: environment.keycloak.realm,
      clientId: environment.keycloak.clientId,
    });

    const authenticated = await this.keycloak.init({
      onLoad: 'login-required',
      checkLoginIframe: false,
      pkceMethod: 'S256',
    });

    this._authenticated.set(authenticated);

    if (authenticated) {
      this.updateUser();
      this.fetchDisplayName();
      this.startTokenRefresh();
    }
  }

  private fetchDisplayName(): void {
    this.api.getMe().subscribe({
      next: (me) => {
        const current = this._user();
        if (current) this._user.set({ ...current, displayName: me.displayName });
      },
      error: () => {
        // Fallback: keep the Keycloak token display name
      },
    });
  }

  private updateUser(): void {
    if (!this.keycloak?.tokenParsed) return;

    const token = this.keycloak.tokenParsed;
    this._user.set({
      keycloakId: token['sub'] ?? '',
      username: (token['preferred_username'] as string) ?? '',
      displayName:
        (token['name'] as string) ??
        (token['preferred_username'] as string) ??
        '',
      email: token['email'] as string | undefined,
      roles: (token['realm_access'] as { roles?: string[] })?.roles ?? [],
    });
  }

  private startTokenRefresh(): void {
    // Refresh token every 60 seconds
    this.refreshInterval = setInterval(async () => {
      try {
        await this.keycloak?.updateToken(30);
        // Only refresh non-displayName fields from the token
        if (this.keycloak?.tokenParsed) {
          const current = this._user();
          const token = this.keycloak.tokenParsed;
          if (current) {
            this._user.set({
              ...current,
              keycloakId: token['sub'] ?? '',
              username: (token['preferred_username'] as string) ?? '',
              email: token['email'] as string | undefined,
              roles: (token['realm_access'] as { roles?: string[] })?.roles ?? [],
            });
          }
        }
      } catch {
        // Token refresh failed
        console.warn('[Auth] Token refresh failed');
      }
    }, 60_000);
  }

  private stopTokenRefresh(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
      this.refreshInterval = null;
    }
  }
}
