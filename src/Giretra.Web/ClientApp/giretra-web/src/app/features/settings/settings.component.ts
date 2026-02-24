import { Component, signal, inject } from '@angular/core';
import { Router } from '@angular/router';
import { LucideAngularModule, ChevronLeft, Users, Ban, Trophy, Settings } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { ProfileSectionComponent } from './components/profile-section.component';
import { FriendsSectionComponent } from './components/friends-section.component';
import { BlockedSectionComponent } from './components/blocked-section.component';
import { MatchHistorySectionComponent } from './components/match-history-section.component';

type Tab = 'profile' | 'friends' | 'blocked' | 'history';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    LucideAngularModule,
    TranslocoDirective,
    ProfileSectionComponent,
    FriendsSectionComponent,
    BlockedSectionComponent,
    MatchHistorySectionComponent,
  ],
  template: `
    <div class="settings-shell" *transloco="let t">
      <header class="settings-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()" title="Back to home">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="SettingsIcon" [size]="18"></i-lucide>
            {{ t('settings.title') }}
          </h1>
        </div>
      </header>

      <main class="settings-main">
        <div class="settings-inner">
          <!-- Tab bar -->
          <nav class="tab-bar">
            <button
              class="tab"
              [class.tab-active]="activeTab() === 'profile'"
              (click)="activeTab.set('profile')"
            >
              <i-lucide [img]="SettingsIcon" [size]="14"></i-lucide>
              {{ t('settings.tabs.profile') }}
            </button>
            <button
              class="tab"
              [class.tab-active]="activeTab() === 'friends'"
              (click)="activeTab.set('friends')"
            >
              <i-lucide [img]="UsersIcon" [size]="14"></i-lucide>
              {{ t('settings.tabs.friends') }}
            </button>
            <button
              class="tab"
              [class.tab-active]="activeTab() === 'blocked'"
              (click)="activeTab.set('blocked')"
            >
              <i-lucide [img]="BanIcon" [size]="14"></i-lucide>
              {{ t('settings.tabs.blocked') }}
            </button>
            <button
              class="tab"
              [class.tab-active]="activeTab() === 'history'"
              (click)="activeTab.set('history')"
            >
              <i-lucide [img]="TrophyIcon" [size]="14"></i-lucide>
              {{ t('settings.tabs.history') }}
            </button>
          </nav>

          <!-- Tab content -->
          <div class="tab-content">
            @switch (activeTab()) {
              @case ('profile') {
                <app-profile-section />
              }
              @case ('friends') {
                <app-friends-section />
              }
              @case ('blocked') {
                <app-blocked-section />
              }
              @case ('history') {
                <app-match-history-section />
              }
            }
          </div>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .settings-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }
    .settings-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:0.75rem 1rem; flex-shrink:0; }
    .header-inner { max-width:720px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { display:flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:var(--radius); border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.08); }
    .header-title { display:flex; align-items:center; gap:0.5rem; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); margin:0; }
    .settings-main { flex:1; padding:1.25rem 1rem; }
    .settings-inner { max-width:720px; margin:0 auto; display:flex; flex-direction:column; gap:1.25rem; }
    .tab-bar { display:flex; gap:0.25rem; background:hsl(var(--secondary)); border-radius:var(--radius); padding:0.25rem; }
    .tab { flex:1; display:flex; align-items:center; justify-content:center; gap:0.375rem; padding:0.5rem 0.75rem; border-radius:calc(var(--radius) - 2px); border:none; background:transparent; color:hsl(var(--muted-foreground)); font-size:0.8125rem; font-weight:500; cursor:pointer; transition:all 0.15s ease; }
    .tab:hover { color:hsl(var(--foreground)); }
    .tab-active { background:hsl(var(--card)); color:hsl(var(--foreground)); font-weight:600; box-shadow:0 1px 3px rgba(0,0,0,0.15); }
    .tab-content { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:var(--radius); padding:1.25rem; }
    @media (max-width:480px) {
      .tab { font-size:0.75rem; padding:0.375rem 0.5rem; }
    }
  `],
})
export class SettingsComponent {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly UsersIcon = Users;
  readonly BanIcon = Ban;
  readonly TrophyIcon = Trophy;
  readonly SettingsIcon = Settings;

  private readonly router = inject(Router);

  readonly activeTab = signal<Tab>('profile');

  goBack(): void {
    this.router.navigate(['/']);
  }
}
