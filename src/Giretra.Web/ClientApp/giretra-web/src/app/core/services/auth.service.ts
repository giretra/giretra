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

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly api = inject(ApiService);
  private keycloak: Keycloak | null = null;
  private refreshInterval: ReturnType<typeof setInterval> | null = null;

  private readonly _authenticated = signal(false);
  private readonly _user = signal<AuthUser | null>(null);

  readonly authenticated = this._authenticated.asReadonly();
  readonly user = this._user.asReadonly();
  readonly hasUser = computed(() => !!this._user());

  async init(): Promise<void> {
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

  async getToken(): Promise<string> {
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
    this.stopTokenRefresh();
    this.keycloak?.logout({ redirectUri: window.location.origin });
  }

  updateLocalDisplayName(newName: string): void {
    const current = this._user();
    if (current) this._user.set({ ...current, displayName: newName });
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
