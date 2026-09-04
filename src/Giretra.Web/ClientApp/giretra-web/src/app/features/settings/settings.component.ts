import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../../core/services/auth.service';
import { PendingFriendsService } from '../../core/services/pending-friends.service';
import { ProfileSectionComponent } from './components/profile-section.component';
import { FriendsSectionComponent } from './components/friends-section.component';
import { BlockedSectionComponent } from './components/blocked-section.component';
import { MatchHistorySectionComponent } from './components/match-history-section.component';

type Section = 'profile' | 'friends' | 'blocked' | 'history';

interface MenuEntry {
  key: Section;
  icon: string;
  labelKey: string;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    TranslocoDirective,
    AvatarModule,
    TagModule,
    ProfileSectionComponent,
    FriendsSectionComponent,
    BlockedSectionComponent,
    MatchHistorySectionComponent,
  ],
  template: `
    <div class="g-card settings" *transloco="let t">
      <!-- Side menu (desktop) -->
      <aside class="side">
        <div class="side-head">
          @if (auth.user(); as user) {
            <p-avatar [label]="user.displayName.charAt(0).toUpperCase()" shape="circle" size="large" styleClass="side-avatar" />
            <div class="side-who">
              <h1 class="side-title">{{ t('settings.title') }}</h1>
              <span class="side-name">{{ user.displayName }}</span>
            </div>
          } @else {
            <h1 class="side-title">{{ t('settings.title') }}</h1>
          }
        </div>
        <nav class="side-menu" [attr.aria-label]="t('settings.menu')">
          <span class="side-menu-label">{{ t('settings.menu') }}</span>
          @for (item of menu; track item.key) {
            <button type="button" class="menu-btn" [class.active]="section() === item.key" (click)="select(item.key)">
              <i [class]="item.icon"></i>
              <span>{{ t(item.labelKey) }}</span>
              @if (item.key === 'friends' && pendingFriends.count() > 0) {
                <p-tag [value]="pendingFriends.count().toString()" severity="danger" [rounded]="true" />
              }
            </button>
          }
        </nav>
      </aside>

      <!-- Chip menu (mobile) -->
      <div class="chips">
        @for (item of menu; track item.key) {
          <button type="button" class="chip" [class.active]="section() === item.key" (click)="select(item.key)">
            <i [class]="item.icon"></i>
            <span>{{ t(item.labelKey) }}</span>
            @if (item.key === 'friends' && pendingFriends.count() > 0) {
              <span class="chip-dot"></span>
            }
          </button>
        }
      </div>

      <!-- Panel -->
      <div class="panel">
        @switch (section()) {
          @case ('profile') { <app-profile-section /> }
          @case ('friends') { <app-friends-section /> }
          @case ('blocked') { <app-blocked-section /> }
          @case ('history') { <app-match-history-section /> }
        }
      </div>
    </div>
  `,
  styles: [`
    .settings { display:flex; flex-direction:column; padding:0; overflow:hidden; min-height:32rem; }
    @media (min-width:1200px) { .settings { flex-direction:row; } }

    .side { display:none; }
    @media (min-width:1200px) { .side { display:flex; flex-direction:column; width:20rem; flex-shrink:0; border-right:1px solid var(--surface-border); } }
    .side-head { display:flex; align-items:center; gap:0.875rem; padding:1.25rem 1.5rem; border-bottom:1px solid var(--surface-border); }
    :host ::ng-deep .side-avatar { background:color-mix(in srgb, var(--p-primary-color) 22%, transparent); color:var(--p-primary-300); font-weight:700; }
    .side-who { display:flex; flex-direction:column; min-width:0; }
    .side-title { margin:0; font-size:1.125rem; font-weight:500; }
    .side-name { font-size:0.875rem; color:var(--text-color-secondary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .side-menu { display:flex; flex-direction:column; gap:0.375rem; padding:1.5rem; }
    .side-menu-label { font-size:0.875rem; font-weight:500; color:var(--text-color-secondary); margin-bottom:0.5rem; }
    .menu-btn { display:flex; align-items:center; gap:0.625rem; padding:0.625rem 0.75rem; border:none; border-radius:0.75rem; background:transparent; color:var(--text-color-secondary); font-size:1rem; text-align:left; cursor:pointer; transition:background-color var(--transition-duration), color var(--transition-duration); }
    .menu-btn span { flex:1; }
    .menu-btn:hover { background:var(--surface-hover); color:var(--text-color); }
    .menu-btn.active { background:var(--p-primary-color); color:var(--p-primary-contrast-color); font-weight:500; box-shadow:0 1px 2px rgba(0,0,0,0.2); }

    .chips { display:flex; gap:0.5rem; padding:1rem 1.25rem; overflow-x:auto; border-bottom:1px solid var(--surface-border); }
    @media (min-width:1200px) { .chips { display:none; } }
    .chip { position:relative; display:inline-flex; align-items:center; gap:0.5rem; padding:0.5rem 1rem; border:none; border-radius:0.75rem; background:var(--p-surface-800); color:var(--text-color-secondary); font-size:0.875rem; white-space:nowrap; cursor:pointer; }
    .chip.active { background:var(--p-primary-color); color:var(--p-primary-contrast-color); font-weight:500; }
    .chip-dot { width:0.5rem; height:0.5rem; border-radius:50%; background:var(--p-red-500); }

    .panel { flex:1; min-width:0; display:flex; flex-direction:column; }
  `],
})
export class SettingsComponent {
  readonly auth = inject(AuthService);
  readonly pendingFriends = inject(PendingFriendsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly menu: MenuEntry[] = [
    { key: 'profile', icon: 'pi pi-user', labelKey: 'settings.tabs.profile' },
    { key: 'friends', icon: 'pi pi-users', labelKey: 'settings.tabs.friends' },
    { key: 'blocked', icon: 'pi pi-ban', labelKey: 'settings.tabs.blocked' },
    { key: 'history', icon: 'pi pi-history', labelKey: 'settings.tabs.history' },
  ];

  readonly section = signal<Section>(this.initialSection());

  select(section: Section): void {
    this.section.set(section);
    this.router.navigate([], { queryParams: { section }, replaceUrl: true });
  }

  private initialSection(): Section {
    const requested = this.route.snapshot.queryParamMap.get('section');
    return this.menu.some((m) => m.key === requested) ? (requested as Section) : 'profile';
  }
}
