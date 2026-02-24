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
          [title]="lang.label"
          (click)="setLang(lang.code)"
        >
          {{ lang.flag }}
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
    }

    .lang-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 1.5rem;
      height: 1.5rem;
      font-size: 0.875rem;
      line-height: 1;
      border: none;
      border-radius: 9999px;
      background: transparent;
      cursor: pointer;
      transition: all 0.15s ease;
      opacity: 0.5;
    }

    .lang-btn:hover:not(.active) {
      opacity: 0.8;
      background: hsl(var(--muted) / 0.3);
    }

    .lang-btn.active {
      opacity: 1;
      background: hsl(var(--primary) / 0.15);
    }
  `],
})
export class LanguageSwitcherComponent {
  private readonly transloco = inject(TranslocoService);

  readonly langs = [
    { code: 'en', flag: '\u{1F1EC}\u{1F1E7}', label: 'English' },
    { code: 'fr', flag: '\u{1F1EB}\u{1F1F7}', label: 'Fran\u00e7ais' },
    { code: 'mg', flag: '\u{1F1F2}\u{1F1EC}', label: 'Malagasy' },
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
