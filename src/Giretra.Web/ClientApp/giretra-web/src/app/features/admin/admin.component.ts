import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LucideAngularModule, Shield, Users, Dices, ChevronRight } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <div class="admin-inner" *transloco="let t">
      <div class="page-head">
          <h1 class="header-title">
            <i-lucide [img]="ShieldIcon" [size]="18"></i-lucide>
            {{ t('admin.title') }}
          </h1>
          <span class="role-badge">{{ t('admin.moderatorBadge') }}</span>
      </div>

      <p class="subtitle">{{ t('admin.subtitle') }}</p>

      <div class="tools-grid">
        <a class="tool-card" (click)="goToUsers()">
          <span class="tool-icon">
            <i-lucide [img]="UsersIcon" [size]="20" [strokeWidth]="2"></i-lucide>
          </span>
          <span class="tool-text">
            <span class="tool-title">{{ t('admin.tools.users.title') }}</span>
            <span class="tool-desc">{{ t('admin.tools.users.desc') }}</span>
          </span>
          <i-lucide [img]="ChevronRightIcon" [size]="16" [strokeWidth]="2" class="tool-arrow"></i-lucide>
        </a>
        <a class="tool-card" (click)="goToGames()">
          <span class="tool-icon">
            <i-lucide [img]="DicesIcon" [size]="20" [strokeWidth]="2"></i-lucide>
          </span>
          <span class="tool-text">
            <span class="tool-title">{{ t('admin.tools.games.title') }}</span>
            <span class="tool-desc">{{ t('admin.tools.games.desc') }}</span>
          </span>
          <i-lucide [img]="ChevronRightIcon" [size]="16" [strokeWidth]="2" class="tool-arrow"></i-lucide>
        </a>
      </div>
    </div>
  `,
  styles: [`

    /* Header */
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .role-badge { margin-left:auto; font-size:0.6875rem; font-weight:600; color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); padding:0.125rem 0.625rem; border-radius:9999px; text-transform:uppercase; letter-spacing:0.06em; }

    /* Main */
    .admin-inner { }
    .page-head { display:flex; align-items:center; gap:0.75rem; flex-wrap:wrap; margin-bottom:1rem; }
    .subtitle { margin:0 0 1rem; font-size:0.8125rem; color:hsl(var(--muted-foreground)); }

    /* Tools grid */
    .tools-grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(18rem, 1fr)); gap:1rem; }
    .tool-card { display:flex; align-items:center; gap:0.875rem; background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:1rem 1.125rem; cursor:pointer; transition:all 0.15s ease; }
    .tool-card:hover { border-color:hsl(var(--foreground)/0.25); background:hsl(var(--foreground)/0.02); }
    .tool-icon { display:flex; align-items:center; justify-content:center; width:2.5rem; height:2.5rem; border-radius:0.625rem; background:hsl(var(--gold)/0.12); color:hsl(var(--gold)); flex-shrink:0; }
    .tool-text { display:flex; flex-direction:column; gap:0.125rem; min-width:0; }
    .tool-title { font-size:0.875rem; font-weight:700; color:hsl(var(--foreground)); }
    .tool-desc { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .tool-arrow { margin-left:auto; color:hsl(var(--muted-foreground)); flex-shrink:0; }
  `],
})
export class AdminComponent {
  readonly ShieldIcon = Shield;
  readonly UsersIcon = Users;
  readonly DicesIcon = Dices;
  readonly ChevronRightIcon = ChevronRight;

  private readonly router = inject(Router);
  readonly auth = inject(AuthService);


  goToUsers(): void {
    this.router.navigate(['/admin/users']);
  }

  goToGames(): void {
    this.router.navigate(['/admin/games']);
  }
}
