import { Component, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  template: `
    <div class="lang-switcher">
      @for (lang of langs; track lang.code) {
        <button
          class="lang-btn"
          [class.active]="activeLang() === lang.code"
          (click)="setLang(lang.code)"
        >
          {{ lang.label }}
        </button>
      }
    </div>
  `,
  styles: [`
    .lang-switcher {
      display: flex;
      background: hsl(var(--muted) / 0.3);
      border-radius: 9999px;
      padding: 0.125rem;
      gap: 0.0625rem;
    }

    .lang-btn {
      padding: 0.125rem 0.5rem;
      font-size: 0.625rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      border: none;
      border-radius: 9999px;
      background: transparent;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
      text-transform: uppercase;
    }

    .lang-btn:hover:not(.active) {
      color: hsl(var(--foreground));
      background: hsl(var(--muted) / 0.3);
    }

    .lang-btn.active {
      background: hsl(var(--primary) / 0.2);
      color: hsl(var(--primary));
    }
  `],
})
export class LanguageSwitcherComponent {
  private readonly transloco = inject(TranslocoService);

  readonly langs = [
    { code: 'en', label: 'EN' },
    { code: 'fr', label: 'FR' },
    { code: 'mg', label: 'MG' },
  ];

  readonly activeLang = this.transloco.langChanges$
    ? () => this.transloco.getActiveLang()
    : () => 'en';

  setLang(code: string): void {
    this.transloco.setActiveLang(code);
    localStorage.setItem('giretra-lang', code);
    document.documentElement.lang = code;
  }
}
