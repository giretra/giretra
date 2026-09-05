import { Component, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: '[app-footer]',
  standalone: true,
  imports: [RouterModule, TranslocoPipe],
  template: `
    <div class="footer-inner">
      <div class="footer-links">
        <a href="https://www.giretra.com" target="_blank" rel="noopener noreferrer" class="footer-link">
          {{ 'layout.footer.website' | transloco }}
        </a>
        <span class="footer-dot"></span>
        <a href="https://github.com/giretra" target="_blank" rel="noopener noreferrer" class="footer-link">
          <i class="pi pi-github"></i> {{ 'layout.footer.source' | transloco }}
        </a>
        <span class="footer-dot"></span>
        <a [routerLink]="['/feedback']" [queryParams]="{ from: router.url }" class="footer-link">
          <i class="pi pi-lightbulb"></i> {{ 'layout.feedback' | transloco }}
        </a>
        <span class="footer-dot"></span>
        <a routerLink="/leaderboard" class="footer-link footer-link-gold">
          <i class="pi pi-trophy"></i> {{ 'home.bestPlayers' | transloco }}
        </a>
      </div>
      <span class="footer-copy">&copy; {{ currentYear }} Giretra</span>
    </div>
  `,
  host: {
    class: 'layout-footer',
  },
})
export class AppFooter {
  readonly router = inject(Router);

  readonly currentYear = new Date().getFullYear();
}
