import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LucideAngularModule, ChevronLeft, Shield, Wrench } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <div class="admin-shell" *transloco="let t">
      <header class="admin-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()" title="Back to home">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="ShieldIcon" [size]="18"></i-lucide>
            {{ t('admin.title') }}
          </h1>
          <span class="role-badge">{{ t('admin.moderatorBadge') }}</span>
        </div>
      </header>

      <main class="admin-main">
        <div class="admin-inner">
          <p class="subtitle">{{ t('admin.subtitle') }}</p>

          <div class="empty-panel">
            <span class="empty-icon">
              <i-lucide [img]="WrenchIcon" [size]="28" [strokeWidth]="1.75"></i-lucide>
            </span>
            <span class="empty-title">{{ t('admin.empty.title') }}</span>
            <span class="empty-text">{{ t('admin.empty.text') }}</span>
          </div>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .admin-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }

    /* Header */
    .admin-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:1rem; flex-shrink:0; }
    .header-inner { max-width:1200px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { display:flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:0.5rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .role-badge { margin-left:auto; font-size:0.6875rem; font-weight:600; color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); padding:0.125rem 0.625rem; border-radius:9999px; text-transform:uppercase; letter-spacing:0.06em; }

    /* Main */
    .admin-main { flex:1; padding:1rem; }
    .admin-inner { max-width:1200px; margin:0 auto; }
    .subtitle { margin:0 0 1rem; font-size:0.8125rem; color:hsl(var(--muted-foreground)); }

    /* Empty state */
    .empty-panel { display:flex; flex-direction:column; align-items:center; gap:0.5rem; background:hsl(var(--card)); border:1px dashed hsl(var(--border)); border-radius:0.75rem; padding:3rem 1.5rem; text-align:center; }
    .empty-icon { display:flex; align-items:center; justify-content:center; width:3.5rem; height:3.5rem; border-radius:50%; background:hsl(var(--muted)/0.5); color:hsl(var(--muted-foreground)); margin-bottom:0.25rem; }
    .empty-title { font-size:0.9375rem; font-weight:600; color:hsl(var(--foreground)); }
    .empty-text { font-size:0.8125rem; color:hsl(var(--muted-foreground)); max-width:26rem; }
  `],
})
export class AdminComponent {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly ShieldIcon = Shield;
  readonly WrenchIcon = Wrench;

  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  goBack(): void {
    this.router.navigate(['/']);
  }
}
