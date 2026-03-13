import { Injectable, signal } from '@angular/core';

const FULLSCREEN_DISMISSED_KEY = 'giretra-fullscreen-dismissed';

@Injectable({ providedIn: 'root' })
export class FullscreenService {
  private readonly _isFullscreen = signal(!!document.fullscreenElement);
  readonly isFullscreen = this._isFullscreen.asReadonly();

  constructor() {
    document.addEventListener('fullscreenchange', () => {
      this._isFullscreen.set(!!document.fullscreenElement);
    });
  }

  get isMobile(): boolean {
    return window.innerWidth < 640;
  }

  get isFullscreenSupported(): boolean {
    return !!document.documentElement.requestFullscreen;
  }

  get wasPromptDismissed(): boolean {
    return localStorage.getItem(FULLSCREEN_DISMISSED_KEY) === 'true';
  }

  /** Whether we should show the fullscreen suggestion to the user. */
  shouldPrompt(): boolean {
    return this.isMobile && this.isFullscreenSupported && !this._isFullscreen() && !this.wasPromptDismissed;
  }

  dismissPrompt(): void {
    localStorage.setItem(FULLSCREEN_DISMISSED_KEY, 'true');
  }

  async enterFullscreen(): Promise<void> {
    if (!this.isFullscreenSupported || this._isFullscreen()) return;
    try {
      await document.documentElement.requestFullscreen();
    } catch {
      // Fullscreen request denied by browser
    }
  }

  async exitFullscreen(): Promise<void> {
    if (!document.fullscreenElement) return;
    try {
      await document.exitFullscreen();
    } catch {
      // Exit fullscreen failed
    }
  }

  async toggle(): Promise<void> {
    if (this._isFullscreen()) {
      await this.exitFullscreen();
    } else {
      await this.enterFullscreen();
    }
  }
}
