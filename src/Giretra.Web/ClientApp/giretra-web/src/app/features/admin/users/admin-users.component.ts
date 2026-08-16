import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, Subscription } from 'rxjs';
import { ApiService, AdminUserEntry } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  LucideAngularModule,
  ChevronLeft,
  Users,
  Search,
  Ban,
  Undo2,
  Eraser,
  ImageOff,
  ShieldAlert,
  Layers,
} from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, DatePipe],
  template: `
    <div class="au-shell" *transloco="let t">
      <header class="au-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()" title="Back to admin">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="UsersIcon" [size]="18"></i-lucide>
            {{ t('adminUsers.title') }}
          </h1>
          @if (totalCount() > 0) {
            <span class="count-badge">{{ t('adminUsers.userCount', { count: totalCount() }) }}</span>
          }
        </div>
      </header>

      <main class="au-main">
        <div class="au-inner">
          <div class="search-bar">
            <i-lucide [img]="SearchIcon" [size]="14" class="search-icon"></i-lucide>
            <input
              class="search-input"
              type="text"
              [placeholder]="t('adminUsers.searchPlaceholder')"
              [value]="search()"
              (input)="onSearchInput($event)"
            />
          </div>

          @if (loading()) {
            <div class="loading-state">{{ t('common.loading') }}</div>
          } @else if (users().length === 0) {
            <div class="empty-state">{{ t('adminUsers.noUsers') }}</div>
          } @else {
            <div class="table-panel">
              <div class="row row-header">
                <div class="col-user">{{ t('adminUsers.columns.user') }}</div>
                <div class="col-email">{{ t('adminUsers.columns.email') }}</div>
                <div class="col-role">{{ t('adminUsers.columns.role') }}</div>
                <div class="col-num">{{ t('adminUsers.columns.elo') }}</div>
                <div class="col-num">{{ t('adminUsers.columns.games') }}</div>
                <div class="col-num" [title]="t('adminUsers.blockedByHint')">{{ t('adminUsers.columns.blockedBy') }}</div>
                <div class="col-date">{{ t('adminUsers.columns.lastLogin') }}</div>
                <div class="col-actions"></div>
              </div>

              @for (u of users(); track u.id) {
                <div class="row" [class.row-banned]="u.isBanned">
                  <div
                    class="col-user"
                    [class.col-user-link]="!!u.playerId"
                    (click)="goToHighlights(u)"
                    [title]="u.playerId ? t('adminUsers.viewStats') : ''"
                  >
                    @if (u.avatarUrl) {
                      <img class="avatar" [src]="u.avatarUrl" [alt]="u.displayName" />
                    } @else {
                      <span class="avatar avatar-placeholder">{{ u.displayName.charAt(0).toUpperCase() }}</span>
                    }
                    <span class="name-group">
                      <span class="entry-name">
                        {{ u.displayName }}
                        @if (u.isBanned) {
                          <span class="banned-badge" [title]="u.banReason || ''">{{ t('adminUsers.banned') }}</span>
                        }
                      </span>
                      <span class="entry-username">{{ u.username }}</span>
                    </span>
                  </div>
                  <div class="col-email">{{ u.email || '–' }}</div>
                  <div class="col-role">
                    @if (u.role !== 'Normal') {
                      <span class="role-badge" [class.role-admin]="u.role === 'Admin'">{{ t('adminUsers.roles.' + u.role.toLowerCase()) }}</span>
                    }
                  </div>
                  <div class="col-num">{{ u.eloRating ?? '–' }}</div>
                  <div class="col-num">{{ u.gamesPlayed ?? '–' }}</div>
                  <div class="col-num" [class.col-warn]="u.blockedByCount >= 3">{{ u.blockedByCount }}</div>
                  <div class="col-date">{{ u.lastLoginAt ? (u.lastLoginAt | date: 'MMM d, y') : '–' }}</div>
                  <div class="col-actions">
                    <button class="action-btn" (click)="goToGames(u)" [title]="t('adminUsers.viewGames')">
                      <i-lucide [img]="LayersIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                    </button>
                    @if (u.customDisplayName) {
                      <button class="action-btn" (click)="clearDisplayName(u)" [title]="t('adminUsers.actions.clearName')">
                        <i-lucide [img]="EraserIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                      </button>
                    }
                    @if (u.avatarUrl) {
                      <button class="action-btn" (click)="removeAvatar(u)" [title]="t('adminUsers.actions.removeAvatar')">
                        <i-lucide [img]="ImageOffIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                      </button>
                    }
                    @if (u.isBanned) {
                      <button class="action-btn action-unban" (click)="unban(u)" [title]="t('adminUsers.actions.unban')">
                        <i-lucide [img]="Undo2Icon" [size]="14" [strokeWidth]="2"></i-lucide>
                      </button>
                    } @else if (u.role === 'Normal' && u.id !== currentUserId()) {
                      <button class="action-btn action-ban" (click)="openBanDialog(u)" [title]="t('adminUsers.actions.ban')">
                        <i-lucide [img]="BanIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                      </button>
                    }
                  </div>
                </div>
              }
            </div>

            @if (totalPages() > 1) {
              <div class="pagination">
                <button class="page-btn" [disabled]="page() <= 1" (click)="setPage(page() - 1)">{{ t('adminUsers.prev') }}</button>
                <span class="page-info">{{ t('adminUsers.pageInfo', { page: page(), totalPages: totalPages() }) }}</span>
                <button class="page-btn" [disabled]="page() >= totalPages()" (click)="setPage(page() + 1)">{{ t('adminUsers.next') }}</button>
              </div>
            }
          }
        </div>
      </main>

      <!-- Ban dialog -->
      @if (banTarget(); as target) {
        <div class="dialog-backdrop" (click)="closeBanDialog()">
          <div class="dialog" (click)="$event.stopPropagation()">
            <h2 class="dialog-title">
              <i-lucide [img]="ShieldAlertIcon" [size]="16"></i-lucide>
              {{ t('adminUsers.banDialog.title', { name: target.displayName }) }}
            </h2>
            <textarea
              class="dialog-reason"
              rows="3"
              [placeholder]="t('adminUsers.banDialog.reasonPlaceholder')"
              [value]="banReason()"
              (input)="onReasonInput($event)"
            ></textarea>
            <div class="dialog-actions">
              <button class="dialog-btn" (click)="closeBanDialog()">{{ t('common.cancel') }}</button>
              <button class="dialog-btn dialog-btn-danger" [disabled]="acting()" (click)="confirmBan()">
                {{ t('adminUsers.banDialog.confirm') }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .au-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }

    /* Header */
    .au-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:1rem; flex-shrink:0; }
    .header-inner { max-width:1200px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { display:flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:0.5rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .count-badge { margin-left:auto; font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.625rem; border-radius:9999px; }

    /* Main */
    .au-main { flex:1; padding:1rem; }
    .au-inner { max-width:1200px; margin:0 auto; }

    .loading-state, .empty-state { text-align:center; padding:3rem 1rem; color:hsl(var(--muted-foreground)); font-size:0.875rem; }

    /* Search */
    .search-bar { position:relative; margin-bottom:1rem; }
    .search-icon { position:absolute; left:0.75rem; top:50%; transform:translateY(-50%); color:hsl(var(--muted-foreground)); pointer-events:none; }
    .search-input { width:100%; padding:0.5rem 0.75rem 0.5rem 2.25rem; font-size:0.8125rem; color:hsl(var(--foreground)); background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.5rem; outline:none; }
    .search-input:focus { border-color:hsl(var(--foreground)/0.3); }

    /* Table */
    .table-panel { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:0.75rem 1rem; overflow-x:auto; }
    .row { display:flex; align-items:center; padding:0.5rem 0.25rem; gap:0.5rem; min-width:56rem; }
    .row:not(.row-header) { border-top:1px solid hsl(var(--border)/0.5); }
    .row-header { font-size:0.625rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; color:hsl(var(--muted-foreground)); }
    .row-banned { opacity:0.75; background:hsl(0 60% 50% / 0.04); }

    .col-user { flex:1.4; min-width:11rem; display:flex; align-items:center; gap:0.5rem; }
    .col-user-link { cursor:pointer; }
    .col-user-link:hover .entry-name { color:hsl(var(--gold)); }
    .col-email { flex:1; min-width:8rem; font-size:0.75rem; color:hsl(var(--muted-foreground)); overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .col-role { width:6rem; flex-shrink:0; }
    .col-num { width:4rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .col-warn { color:hsl(0 70% 55%); font-weight:700; }
    .col-date { width:6.5rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); white-space:nowrap; }
    .col-actions { width:8.5rem; flex-shrink:0; display:flex; justify-content:flex-end; gap:0.25rem; }

    .avatar { width:1.75rem; height:1.75rem; border-radius:50%; flex-shrink:0; object-fit:cover; }
    .avatar-placeholder { display:inline-flex; align-items:center; justify-content:center; background:hsl(var(--muted)); color:hsl(var(--muted-foreground)); font-size:0.75rem; font-weight:700; }
    .name-group { display:flex; flex-direction:column; min-width:0; }
    .entry-name { font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; display:flex; align-items:center; gap:0.375rem; }
    .entry-username { font-size:0.6875rem; color:hsl(var(--muted-foreground)); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }

    .banned-badge { font-size:0.5625rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; color:hsl(0 70% 55%); background:hsl(0 70% 50% / 0.12); padding:0.0625rem 0.4375rem; border-radius:9999px; }
    .role-badge { font-size:0.625rem; font-weight:600; text-transform:uppercase; letter-spacing:0.06em; color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); padding:0.125rem 0.5rem; border-radius:9999px; }
    .role-admin { color:hsl(265 70% 65%); background:hsl(265 70% 60% / 0.12); }

    .action-btn { display:flex; align-items:center; justify-content:center; width:1.75rem; height:1.75rem; border-radius:0.375rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .action-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.08); }
    .action-ban:hover { color:hsl(0 70% 55%); background:hsl(0 70% 50% / 0.1); }
    .action-unban:hover { color:hsl(140 60% 45%); background:hsl(140 60% 45% / 0.1); }

    /* Pagination */
    .pagination { display:flex; align-items:center; justify-content:center; gap:1rem; margin-top:1rem; }
    .page-btn { font-size:0.75rem; font-weight:600; color:hsl(var(--foreground)); background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.5rem; padding:0.375rem 0.875rem; cursor:pointer; }
    .page-btn:disabled { opacity:0.4; cursor:default; }
    .page-info { font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    /* Ban dialog */
    .dialog-backdrop { position:fixed; inset:0; background:hsl(0 0% 0% / 0.5); display:flex; align-items:center; justify-content:center; z-index:50; padding:1rem; }
    .dialog { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:1.25rem; width:100%; max-width:24rem; display:flex; flex-direction:column; gap:0.75rem; }
    .dialog-title { margin:0; font-size:0.9375rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .dialog-reason { width:100%; resize:vertical; font-size:0.8125rem; font-family:inherit; color:hsl(var(--foreground)); background:hsl(var(--background)); border:1px solid hsl(var(--border)); border-radius:0.5rem; padding:0.5rem 0.75rem; outline:none; }
    .dialog-reason:focus { border-color:hsl(var(--foreground)/0.3); }
    .dialog-actions { display:flex; justify-content:flex-end; gap:0.5rem; }
    .dialog-btn { font-size:0.75rem; font-weight:600; color:hsl(var(--foreground)); background:transparent; border:1px solid hsl(var(--border)); border-radius:0.5rem; padding:0.375rem 0.875rem; cursor:pointer; }
    .dialog-btn-danger { color:hsl(0 0% 100%); background:hsl(0 70% 45%); border-color:hsl(0 70% 45%); }
    .dialog-btn-danger:disabled { opacity:0.6; cursor:default; }

    @media (max-width:640px) {
      .col-email, .col-date { display:none; }
      .row { min-width:34rem; }
    }
  `],
})
export class AdminUsersComponent implements OnInit, OnDestroy {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly UsersIcon = Users;
  readonly SearchIcon = Search;
  readonly BanIcon = Ban;
  readonly Undo2Icon = Undo2;
  readonly EraserIcon = Eraser;
  readonly ImageOffIcon = ImageOff;
  readonly ShieldAlertIcon = ShieldAlert;
  readonly LayersIcon = Layers;

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  private static readonly PAGE_SIZE = 25;
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;
  private queryParamsSub: Subscription | null = null;

  readonly users = signal<AdminUserEntry[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly search = signal('');
  readonly loading = signal(true);
  readonly acting = signal(false);
  readonly banTarget = signal<AdminUserEntry | null>(null);
  readonly banReason = signal('');

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / AdminUsersComponent.PAGE_SIZE)));
  // Admin API entries are keyed by DB user id; the auth token only has the Keycloak
  // id, so self-ban is ultimately prevented server-side. This hides the button when
  // the row is recognizable client-side (matching username).
  readonly currentUserId = computed(() => {
    const username = this.auth.user()?.username;
    return this.users().find((u) => u.username === username)?.id ?? '';
  });

  ngOnInit(): void {
    // The URL is the source of truth for page and search, so browser back/forward
    // restores the listing state.
    this.queryParamsSub = this.route.queryParamMap.subscribe((params) => {
      this.page.set(Math.max(1, parseInt(params.get('page') ?? '1', 10) || 1));
      this.search.set(params.get('q') ?? '');
      this.load();
    });
  }

  ngOnDestroy(): void {
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.queryParamsSub?.unsubscribe();
  }

  goBack(): void {
    this.router.navigate(['/admin']);
  }

  goToGames(user: AdminUserEntry): void {
    this.router.navigate(['/admin/games'], { queryParams: { userId: user.id, name: user.displayName } });
  }

  goToHighlights(user: AdminUserEntry): void {
    if (user.playerId) {
      this.router.navigate(['/highlights', user.playerId]);
    }
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.search.set(value);
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => {
      // replaceUrl keeps typing from flooding the browser history
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { q: value.trim() || null, page: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }, 300);
  }

  setPage(page: number): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page > 1 ? page : null },
      queryParamsHandling: 'merge',
    });
  }

  openBanDialog(user: AdminUserEntry): void {
    this.banReason.set('');
    this.banTarget.set(user);
  }

  closeBanDialog(): void {
    this.banTarget.set(null);
  }

  onReasonInput(event: Event): void {
    this.banReason.set((event.target as HTMLTextAreaElement).value);
  }

  confirmBan(): void {
    const target = this.banTarget();
    if (!target) return;
    this.act(this.api.banUser(target.id, this.banReason().trim() || null), () => this.closeBanDialog());
  }

  unban(user: AdminUserEntry): void {
    if (!confirm(this.transloco.translate('adminUsers.confirmUnban', { name: user.displayName }))) return;
    this.act(this.api.unbanUser(user.id));
  }

  clearDisplayName(user: AdminUserEntry): void {
    if (!confirm(this.transloco.translate('adminUsers.confirmClearName', { name: user.displayName }))) return;
    this.act(this.api.clearUserDisplayName(user.id));
  }

  removeAvatar(user: AdminUserEntry): void {
    if (!confirm(this.transloco.translate('adminUsers.confirmRemoveAvatar', { name: user.displayName }))) return;
    this.act(this.api.removeUserAvatar(user.id));
  }

  private act(request: Observable<void>, onSuccess?: () => void): void {
    if (this.acting()) return;
    this.acting.set(true);
    request.subscribe({
      next: () => {
        this.acting.set(false);
        onSuccess?.();
        this.load();
      },
      error: () => this.acting.set(false),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAdminUsers(this.search().trim() || null, this.page(), AdminUsersComponent.PAGE_SIZE).subscribe({
      next: (res) => {
        this.users.set(res.users);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
