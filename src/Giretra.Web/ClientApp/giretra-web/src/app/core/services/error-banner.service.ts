import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ErrorBannerService {
  private readonly _message = signal<string | null>(null);
  private dismissTimer: ReturnType<typeof setTimeout> | null = null;

  readonly message = this._message.asReadonly();

  show(message: string, duration = 2000): void {
    if (this.dismissTimer) {
      clearTimeout(this.dismissTimer);
    }
    this._message.set(message);
    this.dismissTimer = setTimeout(() => {
      this._message.set(null);
      this.dismissTimer = null;
    }, duration);
  }

  dismiss(): void {
    if (this.dismissTimer) {
      clearTimeout(this.dismissTimer);
      this.dismissTimer = null;
    }
    this._message.set(null);
  }
}
