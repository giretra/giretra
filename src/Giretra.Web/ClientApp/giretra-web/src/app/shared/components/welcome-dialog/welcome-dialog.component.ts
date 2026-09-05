import { Component, inject, output, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { RouterModule } from '@angular/router';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';

const WELCOME_STORAGE_KEY = 'giretra-welcome-done';

@Component({
  selector: 'app-welcome-dialog',
  standalone: true,
  imports: [TranslocoDirective, RouterModule, DialogModule, ButtonModule],
  template: `
    <ng-container *transloco="let t">
      <p-dialog
        [visible]="true"
        [modal]="true"
        [closable]="false"
        [draggable]="false"
        [style]="{ width: '28rem' }"
        [breakpoints]="{ '640px': '95vw' }"
        appendTo="body"
      >
        <ng-template #header>
          <div class="welcome-head">
            <img src="icon-192x192.png" alt="" class="welcome-icon" width="56" height="56" />
            <h2 class="welcome-title">{{ t('welcome.title') }}</h2>
            <p class="welcome-subtitle">{{ t('welcome.subtitle') }}</p>
          </div>
        </ng-template>

        <div class="welcome-body">
          <div class="oss">
            <span class="oss-icon"><i class="pi pi-github"></i></span>
            <div class="oss-text">
              <p>{{ t('welcome.openSourceText') }}</p>
              <a [routerLink]="['/feedback']" (click)="confirm()">
                {{ t('welcome.feedbackLink') }} <i class="pi pi-arrow-right"></i>
              </a>
            </div>
          </div>

          <div class="lang">
            <div class="lang-label">{{ t('welcome.chooseLanguage') }}</div>
            <div class="lang-grid">
              @for (lang of langs; track lang.code) {
                <button
                  type="button"
                  class="lang-tile"
                  [class.selected]="selectedLang() === lang.code"
                  (click)="selectLang(lang.code)"
                >
                  <span class="lang-flag">{{ lang.flag }}</span>
                  <span class="lang-name">{{ lang.label }}</span>
                </button>
              }
            </div>
          </div>
        </div>

        <ng-template #footer>
          <p-button class="w-full" styleClass="w-full" [label]="t('welcome.letsPlay')" icon="pi pi-play" (onClick)="confirm()" />
        </ng-template>
      </p-dialog>
    </ng-container>
  `,
  styles: [`
    .welcome-head { width:100%; display:flex; flex-direction:column; align-items:center; text-align:center; gap:0.25rem; padding-top:0.5rem; }
    .welcome-icon { width:3.5rem; height:3.5rem; margin-bottom:0.5rem; filter:drop-shadow(0 4px 12px rgba(0,0,0,0.3)); }
    .welcome-title { margin:0; font-size:1.375rem; font-weight:700; }
    .welcome-subtitle { margin:0; color:var(--text-color-secondary); font-size:0.9375rem; }
    .welcome-body { display:flex; flex-direction:column; gap:1.25rem; }
    .oss { display:flex; gap:0.875rem; padding:0.875rem 1rem; border-radius:0.875rem; background:var(--p-surface-800); }
    .oss-icon { display:inline-flex; align-items:center; justify-content:center; width:2.25rem; height:2.25rem; border-radius:0.625rem; background:var(--p-surface-700); flex-shrink:0; }
    .oss-text { display:flex; flex-direction:column; gap:0.375rem; font-size:0.875rem; }
    .oss-text p { margin:0; color:var(--text-color); }
    .oss-text a { color:var(--p-primary-400); font-weight:500; }
    .oss-text a i { font-size:0.6875rem; margin-left:0.25rem; }
    .lang-label { font-size:0.75rem; font-weight:600; letter-spacing:0.06em; text-transform:uppercase; color:var(--text-color-secondary); margin-bottom:0.625rem; }
    .lang-grid { display:grid; grid-template-columns:repeat(3, 1fr); gap:0.5rem; }
    .lang-tile { display:flex; flex-direction:column; align-items:center; gap:0.375rem; padding:0.875rem 0.5rem; border:1px solid var(--surface-border); border-radius:0.875rem; background:transparent; color:inherit; cursor:pointer; transition:background-color var(--transition-duration), border-color var(--transition-duration); }
    .lang-tile:hover { background:var(--surface-hover); }
    .lang-tile.selected { border-color:color-mix(in srgb, var(--p-primary-color) 60%, transparent); background:color-mix(in srgb, var(--p-primary-color) 12%, transparent); }
    .lang-flag { font-size:1.5rem; line-height:1; }
    .lang-name { font-size:0.8125rem; font-weight:500; }
  `],
})
export class WelcomeDialogComponent {
  readonly dismissed = output<void>();

  private readonly transloco = inject(TranslocoService);

  readonly langs = [
    { code: 'mg', flag: '\u{1F1F2}\u{1F1EC}', label: 'Malagasy' },
    { code: 'fr', flag: '\u{1F1EB}\u{1F1F7}', label: 'Fran\u00e7ais' },
    { code: 'en', flag: '\u{1F1EC}\u{1F1E7}', label: 'English' },
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
