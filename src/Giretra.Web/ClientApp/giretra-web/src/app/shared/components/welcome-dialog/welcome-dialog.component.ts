import { Component, inject, output, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { LucideAngularModule, Github } from 'lucide-angular';

const WELCOME_STORAGE_KEY = 'giretra-welcome-done';

@Component({
  selector: 'app-welcome-dialog',
  standalone: true,
  imports: [TranslocoDirective, LucideAngularModule],
  template: `
    <ng-container *transloco="let t">
      <div class="backdrop"></div>
      <div class="dialog-container">
        <div class="dialog" (click)="$event.stopPropagation()">
          <!-- Header -->
          <div class="dialog-header">
            <img src="icon-192x192.png" alt="Giretra" class="dialog-icon" width="48" height="48" />
            <h2 class="dialog-title">{{ t('welcome.title') }}</h2>
            <p class="dialog-subtitle">{{ t('welcome.subtitle') }}</p>
          </div>

          <!-- Open-source message -->
          <div class="oss-section">
            <p class="oss-text">{{ t('welcome.openSourceText') }}</p>
            <a
              href="https://github.com/giretra/giretra"
              target="_blank"
              rel="noopener noreferrer"
              class="oss-link"
            >
              <i-lucide [img]="GithubIcon" [size]="16" [strokeWidth]="2"></i-lucide>
              {{ t('welcome.feedbackLink') }}
            </a>
          </div>

          <!-- Language selection -->
          <div class="lang-section">
            <p class="lang-label">{{ t('welcome.chooseLanguage') }}</p>
            <div class="lang-options">
              @for (lang of langs; track lang.code) {
                <button
                  class="lang-option"
                  [class.selected]="selectedLang() === lang.code"
                  (click)="selectLang(lang.code)"
                >
                  <span class="lang-flag">{{ lang.flag }}</span>
                  <span class="lang-name">{{ lang.label }}</span>
                </button>
              }
            </div>
          </div>

          <!-- Confirm button -->
          <button class="confirm-btn" (click)="confirm()">
            {{ t('welcome.letsPlay') }}
          </button>
        </div>
      </div>
    </ng-container>
  `,
  styles: [`
    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(4px);
      z-index: 200;
      animation: fadeIn 0.2s ease;
    }

    .dialog-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 201;
      padding: 1rem;
    }

    .dialog {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 2rem 1.5rem;
      max-width: 420px;
      width: 100%;
      animation: scaleIn 0.25s ease;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    .dialog-header {
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
    }

    .dialog-icon {
      width: 3rem;
      height: 3rem;
      filter: drop-shadow(0 2px 8px rgba(0, 0, 0, 0.3));
    }

    .dialog-title {
      font-family: 'Urbanist', sans-serif;
      font-size: 1.375rem;
      font-weight: 800;
      color: hsl(var(--foreground));
      margin: 0;
    }

    .dialog-subtitle {
      font-size: 0.8125rem;
      color: hsl(var(--muted-foreground));
      margin: 0;
    }

    .oss-section {
      background: hsl(var(--muted) / 0.3);
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .oss-text {
      font-size: 0.8125rem;
      color: hsl(var(--foreground) / 0.85);
      margin: 0;
      line-height: 1.5;
    }

    .oss-link {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--primary));
      text-decoration: none;
      transition: opacity 0.15s ease;
    }

    .oss-link:hover {
      opacity: 0.8;
    }

    .lang-section {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .lang-label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin: 0;
    }

    .lang-options {
      display: flex;
      gap: 0.5rem;
    }

    .lang-option {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
      padding: 0.75rem 0.5rem;
      border: 2px solid hsl(var(--border));
      border-radius: 0.75rem;
      background: transparent;
      cursor: pointer;
      transition: all 0.15s ease;
      color: hsl(var(--foreground));
    }

    .lang-option:hover {
      border-color: hsl(var(--foreground) / 0.3);
      background: hsl(var(--foreground) / 0.04);
    }

    .lang-option.selected {
      border-color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.08);
    }

    .lang-flag {
      font-size: 1.5rem;
      line-height: 1;
    }

    .lang-name {
      font-size: 0.75rem;
      font-weight: 600;
    }

    .confirm-btn {
      width: 100%;
      padding: 0.75rem;
      font-size: 0.9375rem;
      font-weight: 700;
      border: none;
      border-radius: 0.625rem;
      background: hsl(var(--primary));
      color: hsl(var(--primary-foreground));
      cursor: pointer;
      transition: opacity 0.15s ease;
    }

    .confirm-btn:hover {
      opacity: 0.9;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes scaleIn {
      from { opacity: 0; transform: scale(0.95); }
      to { opacity: 1; transform: scale(1); }
    }
  `],
})
export class WelcomeDialogComponent {
  readonly GithubIcon = Github;
  readonly dismissed = output<void>();

  private readonly transloco = inject(TranslocoService);

  readonly langs = [
    { code: 'en', flag: '\u{1F1EC}\u{1F1E7}', label: 'English' },
    { code: 'fr', flag: '\u{1F1EB}\u{1F1F7}', label: 'Fran\u00e7ais' },
    { code: 'mg', flag: '\u{1F1F2}\u{1F1EC}', label: 'Malagasy' },
  ];

  readonly selectedLang = signal(this.transloco.getActiveLang());

  selectLang(code: string): void {
    this.selectedLang.set(code);
    this.transloco.setActiveLang(code);
    localStorage.setItem('giretra-lang', code);
    document.documentElement.lang = code;
  }

  confirm(): void {
    localStorage.setItem(WELCOME_STORAGE_KEY, 'true');
    this.dismissed.emit();
  }

  static shouldShow(): boolean {
    return !localStorage.getItem(WELCOME_STORAGE_KEY);
  }
}
