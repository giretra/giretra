import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import {
  ApiService,
  AchievementShowcaseResponse,
  AchievementShowcaseItem,
} from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LucideAngularModule, ChevronLeft, Award, Star, Lock, ArrowDownWideNarrow } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

type SortMode = 'rarity' | 'name' | 'recent';

@Component({
  selector: 'app-achievements',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <div class="ach-shell" *transloco="let t">
      <header class="ach-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="AwardIcon" [size]="20"></i-lucide>
            {{ t('achievements.page.title') }}
          </h1>
          @if (showcase()) {
            <span class="header-subtitle">{{ t('achievements.page.subtitle', { earned: showcase()!.earnedCount, total: showcase()!.totalCount }) }}</span>
          }
        </div>
      </header>

      <main class="ach-main">
        <div class="ach-inner">
          @if (loading()) {
            <div class="loading-state">
              <div class="loading-spinner"></div>
            </div>
          } @else if (showcase()) {
            <!-- Player name banner (when viewing another player) -->
            @if (isOtherPlayer()) {
              <div class="player-banner">
                <div class="player-avatar">{{ playerInitial() }}</div>
                <span class="player-name">{{ showcase()!.playerName }}</span>
              </div>
            }

            <!-- Progress hero -->
            <section class="hero-card">
              <div class="hero-progress">
                <span class="hero-percent">{{ progressPercent() }}<span class="hero-percent-sign">%</span></span>
                <div class="hero-progress-body">
                  <div class="progress-bar-track">
                    <div class="progress-bar-fill" [style.width.%]="progressPercent()"></div>
                  </div>
                  <div class="progress-stats">
                    <span class="progress-earned">{{ showcase()!.earnedCount }} {{ t('achievements.page.earned') }}</span>
                    <span class="progress-locked">{{ showcase()!.totalCount - showcase()!.earnedCount }} {{ t('achievements.page.locked') }}</span>
                  </div>
                </div>
              </div>
              @if (latestUnlock(); as latest) {
                <div class="hero-latest">
                  <div class="hero-latest-icon">
                    <i-lucide [img]="AwardIcon" [size]="18" [strokeWidth]="1.5"></i-lucide>
                  </div>
                  <div class="hero-latest-info">
                    <span class="hero-latest-label">{{ t('achievements.page.latestUnlock') }}</span>
                    <span class="hero-latest-name">{{ latest.name }}</span>
                    <span class="hero-latest-date">{{ formatDate(latest.earnedAt!) }}</span>
                  </div>
                </div>
              }
            </section>

            <!-- Info text -->
            @if (qualifyingBotsLabel(); as bots) {
              <p class="ach-info-text">{{ t('achievements.page.infoBots', { bots }) }}</p>
            } @else {
              <p class="ach-info-text">{{ t('achievements.page.info') }}</p>
            }

            <!-- Sort toolbar -->
            <div class="toolbar">
              <div class="sort-group" role="group" [attr.aria-label]="t('achievements.page.sortBy')">
                <i-lucide [img]="SortIcon" [size]="14" [strokeWidth]="2" class="sort-icon"></i-lucide>
                @for (s of sortOptions; track s) {
                  <button
                    class="sort-btn"
                    [class.active]="sortBy() === s"
                    [attr.aria-pressed]="sortBy() === s"
                    (click)="sortBy.set(s)"
                  >{{ t('achievements.page.sort.' + s) }}</button>
                }
              </div>
            </div>

            <!-- Earned achievements -->
            @if (earnedAchievements().length > 0) {
              <div class="section-header earned-section-header">
                <i-lucide [img]="AwardIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>{{ t('achievements.page.earned') }}</span>
                <span class="section-count">{{ earnedAchievements().length }}</span>
              </div>
              <div class="tier-grid">
                @for (ach of earnedAchievements(); track ach.code) {
                    <div
                      class="ach-card earned"
                      [class.tier-high]="ach.tier >= 4"
                    >
                      <div class="ach-card-top">
                        <div class="ach-card-icon earned">
                          <i-lucide [img]="AwardIcon" [size]="20" [strokeWidth]="1.5"></i-lucide>
                        </div>
                        <div class="ach-card-info">
                          <span class="ach-card-name">{{ ach.name }}</span>
                          <span class="ach-card-stars">
                            @for (_ of starArray(ach.tier); track $index) {
                              <span class="star filled">&#9733;</span>
                            }
                            @for (_ of starArray(5 - ach.tier); track $index) {
                              <span class="star empty">&#9733;</span>
                            }
                          </span>
                        </div>
                        <span class="earned-badge">{{ t('achievements.page.earned') }}</span>
                      </div>

                      <div class="ach-card-details">
                        <p class="ach-card-desc">{{ t('achievements.desc.' + ach.code) }}</p>
                        <div class="ach-card-meta">
                          <span class="ach-card-category">{{ t('achievements.page.category.' + ach.category) }}</span>
                          @if (ach.earnedAt) {
                            <span class="ach-card-date">{{ t('achievements.page.earnedOn', { date: formatDate(ach.earnedAt) }) }}</span>
                          }
                        </div>
                      </div>
                    </div>
                  }
                </div>
              }

            <!-- Locked achievements -->
            @if (lockedAchievements().length > 0) {
              <div class="section-header locked-section-header">
                <i-lucide [img]="LockIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>{{ t('achievements.page.locked') }}</span>
                <span class="section-count">{{ lockedAchievements().length }}</span>
              </div>
              <div class="tier-grid">
                @for (ach of lockedAchievements(); track ach.code) {
                    <div
                      class="ach-card locked"
                      [class.hidden-ach]="ach.isHidden"
                      [class.tier-high]="ach.tier >= 4"
                    >
                      <div class="ach-card-top">
                        <div class="ach-card-icon">
                          <i-lucide [img]="LockIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                        </div>
                        <div class="ach-card-info">
                          @if (ach.isHidden) {
                            <span class="ach-card-name hidden-text">???</span>
                          } @else {
                            <span class="ach-card-name">{{ ach.name }}</span>
                          }
                          <span class="ach-card-stars">
                            @for (_ of starArray(ach.tier); track $index) {
                              <span class="star filled">&#9733;</span>
                            }
                            @for (_ of starArray(5 - ach.tier); track $index) {
                              <span class="star empty">&#9733;</span>
                            }
                          </span>
                        </div>
                      </div>
                      <div class="ach-card-details">
                        @if (ach.isHidden) {
                          <p class="ach-card-desc hidden-text">{{ t('achievements.page.hiddenDesc') }}</p>
                        } @else {
                          <p class="ach-card-desc">{{ t('achievements.desc.' + ach.code) }}</p>
                        }
                        <div class="ach-card-meta">
                          <span class="ach-card-category">{{ t('achievements.page.category.' + ach.category) }}</span>
                        </div>
                      </div>
                    </div>
                }
              </div>
            }

            @if (showcase()!.achievements.length === 0) {
              <div class="empty-state">
                <i-lucide [img]="AwardIcon" [size]="48" [strokeWidth]="1"></i-lucide>
                <p>{{ t('achievements.page.noAchievements') }}</p>
              </div>
            }
          }
        </div>
      </main>
    </div>
  `,
  styles: [`
    :host { display:block; height:100%; }
    .ach-shell { display:flex; flex-direction:column; min-height:100vh; min-height:100dvh; background:hsl(var(--background)); color:hsl(var(--foreground)); }
    .ach-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:0.75rem 1rem; position:sticky; top:0; z-index:10; }
    .header-inner { max-width:1200px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { background:none; border:none; color:hsl(var(--muted-foreground)); cursor:pointer; padding:0.375rem; border-radius:0.5rem; display:grid; place-items:center; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--muted)/0.5); }
    .header-title { font-size:1.125rem; font-weight:700; margin:0; display:flex; align-items:center; gap:0.5rem; color:hsl(var(--gold)); }
    .header-subtitle { font-size:0.75rem; color:hsl(var(--muted-foreground)); margin-left:auto; font-variant-numeric:tabular-nums; }
    .ach-main { flex:1; overflow-y:auto; padding:1.5rem 1rem; }
    .ach-inner { max-width:1200px; margin:0 auto; display:flex; flex-direction:column; gap:1.25rem; }
    .loading-state { display:flex; justify-content:center; padding:3rem; }
    .loading-spinner { width:2rem; height:2rem; border:2px solid hsl(var(--muted)); border-top-color:hsl(var(--gold)); border-radius:50%; animation:spin 0.8s linear infinite; }
    @keyframes spin { to { transform:rotate(360deg); } }
    .player-banner { display:flex; align-items:center; gap:0.75rem; padding:0.75rem 1rem; background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; }
    .player-avatar { width:2.5rem; height:2.5rem; border-radius:50%; background:hsl(var(--muted)); border:2px solid hsl(var(--gold)/0.4); display:grid; place-items:center; font-size:1rem; font-weight:700; text-transform:uppercase; }
    .player-name { font-weight:600; }
    .hero-card { display:flex; flex-direction:column; gap:1rem; padding:1rem 1.25rem; background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; }
    .hero-progress { display:flex; align-items:center; gap:1rem; flex:1; min-width:0; }
    .hero-percent { font-size:2rem; font-weight:800; line-height:1; color:hsl(var(--gold)); font-variant-numeric:tabular-nums; flex-shrink:0; }
    .hero-percent-sign { font-size:1rem; font-weight:700; color:hsl(var(--gold)/0.7); }
    .hero-progress-body { flex:1; min-width:0; display:flex; flex-direction:column; gap:0.5rem; }
    .progress-bar-track { height:0.5rem; background:hsl(var(--muted)); border-radius:9999px; overflow:hidden; }
    .progress-bar-fill { height:100%; background:linear-gradient(90deg,hsl(var(--gold)),hsl(45 95% 65%)); border-radius:9999px; transition:width 0.6s cubic-bezier(0.2,0,0,1); }
    .progress-stats { display:flex; justify-content:space-between; font-size:0.6875rem; font-weight:500; text-transform:uppercase; letter-spacing:0.04em; }
    .progress-earned { color:hsl(var(--gold)); }
    .progress-locked { color:hsl(var(--muted-foreground)); }
    .hero-latest { display:flex; align-items:center; gap:0.75rem; padding:0.625rem 0.875rem; background:hsl(var(--gold)/0.06); border:1px solid hsl(var(--gold)/0.25); border-radius:0.625rem; }
    .hero-latest-info { display:flex; flex-direction:column; min-width:0; }
    .hero-latest-label { font-size:0.625rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; color:hsl(var(--gold)/0.8); }
    .hero-latest-name { font-size:0.875rem; font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .hero-latest-date { font-size:0.6875rem; color:hsl(var(--muted-foreground)); }
    @media (min-width:720px) {
      .hero-card { flex-direction:row; align-items:center; }
      .hero-latest { max-width:40%; }
    }
    .ach-info-text { font-size:0.8125rem; color:hsl(var(--muted-foreground)); line-height:1.5; margin:0; padding:0.625rem 0.75rem; background:hsl(var(--secondary)/0.5); border:1px solid hsl(var(--border)/0.5); border-radius:0.625rem; }
    .toolbar { display:flex; justify-content:flex-end; }
    .sort-group { display:flex; align-items:center; gap:0.25rem; }
    .sort-icon { color:hsl(var(--muted-foreground)); margin-right:0.25rem; }
    .sort-btn { cursor:pointer; white-space:nowrap; font-weight:600; transition:all 0.15s ease; padding:0.3125rem 0.625rem; border-radius:0.5rem; border:1px solid transparent; background:transparent; color:hsl(var(--muted-foreground)); font-size:0.75rem; }
    .sort-btn:hover { color:hsl(var(--foreground)); }
    .sort-btn.active { background:hsl(var(--secondary)); border-color:hsl(var(--border)); color:hsl(var(--foreground)); }
    .section-header { display:flex; align-items:center; gap:0.5rem; font-size:0.875rem; font-weight:700; text-transform:uppercase; letter-spacing:0.04em; color:hsl(var(--muted-foreground)); }
    .earned-section-header { color:hsl(var(--gold)); }
    .section-count { margin-left:auto; font-size:0.75rem; font-variant-numeric:tabular-nums; }
    .tier-grid { display:grid; grid-template-columns:1fr; gap:0.625rem; }
    @media (min-width:720px) { .tier-grid { grid-template-columns:repeat(2,1fr); } }
    @media (min-width:1024px) { .tier-grid { grid-template-columns:repeat(3,1fr); } }
    .ach-card { display:flex; flex-direction:column; padding:0.75rem; border-radius:0.75rem; border:1px solid hsl(var(--border)/0.6); background:hsl(var(--card)); }
    .ach-card.earned { border-color:hsl(var(--gold)/0.3); background:hsl(var(--gold)/0.04); }
    .ach-card.earned.tier-high { border-color:hsl(var(--gold)/0.5); background:hsl(var(--gold)/0.08); }
    .ach-card.locked { opacity:0.65; }
    .ach-card.hidden-ach { border-style:dashed; }
    .ach-card-top { display:flex; align-items:center; gap:0.625rem; }
    .ach-card-icon, .hero-latest-icon { width:2.25rem; height:2.25rem; border-radius:0.625rem; display:grid; place-items:center; flex-shrink:0; background:hsl(var(--muted)); color:hsl(var(--muted-foreground)); }
    .ach-card-icon.earned, .hero-latest-icon { background:hsl(var(--gold)/0.15); color:hsl(var(--gold)); }
    .ach-card-info { flex:1; min-width:0; display:flex; flex-direction:column; gap:0.125rem; }
    .ach-card-name { font-size:0.9375rem; font-weight:600; line-height:1.25; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
    .hidden-text { color:hsl(var(--muted-foreground)); font-style:italic; }
    .ach-card-stars { display:flex; gap:1px; font-size:0.75rem; line-height:1; }
    .star.filled { color:hsl(var(--gold)); }
    .star.empty { color:hsl(var(--muted)/0.5); }
    .earned-badge { font-size:0.6875rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; padding:0.125rem 0.5rem; border-radius:9999px; background:hsl(var(--gold)/0.15); color:hsl(var(--gold)); white-space:nowrap; flex-shrink:0; }
    .ach-card-details { margin-top:0.625rem; padding-top:0.5rem; border-top:1px solid hsl(var(--border)/0.4); flex:1; display:flex; flex-direction:column; }
    .ach-card-desc { font-size:0.875rem; color:hsl(var(--muted-foreground)); line-height:1.5; margin:0; }
    .ach-card-meta { display:flex; align-items:center; justify-content:space-between; gap:0.5rem; margin-top:auto; padding-top:0.5rem; }
    .ach-card-category { font-size:0.625rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; color:hsl(var(--muted-foreground)/0.7); background:hsl(var(--muted)/0.4); padding:0.125rem 0.4375rem; border-radius:9999px; }
    .ach-card-date { font-size:0.75rem; color:hsl(var(--gold)/0.8); }
    .empty-state { display:flex; flex-direction:column; align-items:center; gap:1rem; padding:3rem 1rem; color:hsl(var(--muted-foreground)); text-align:center; }
    .empty-state p { font-size:0.875rem; margin:0; }
  `],
})
export class AchievementsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);

  readonly ChevronLeftIcon = ChevronLeft;
  readonly AwardIcon = Award;
  readonly StarIcon = Star;
  readonly LockIcon = Lock;
  readonly SortIcon = ArrowDownWideNarrow;

  readonly sortOptions: SortMode[] = ['rarity', 'name', 'recent'];

  readonly loading = signal(true);
  readonly showcase = signal<AchievementShowcaseResponse | null>(null);
  readonly sortBy = signal<SortMode>('rarity');
  private playerId: string | null = null;

  readonly isOtherPlayer = computed(() => !!this.playerId);

  readonly playerInitial = computed(() => {
    const name = this.showcase()?.playerName;
    return name ? name.charAt(0).toUpperCase() : '?';
  });

  readonly progressPercent = computed(() => {
    const s = this.showcase();
    if (!s || s.totalCount === 0) return 0;
    return Math.round((s.earnedCount / s.totalCount) * 100);
  });

  readonly qualifyingBotsLabel = computed(() => {
    const bots = this.showcase()?.qualifyingBots;
    return bots && bots.length > 0 ? bots.join(' & ') : null;
  });

  readonly latestUnlock = computed(() => {
    const s = this.showcase();
    if (!s) return null;
    let latest: AchievementShowcaseItem | null = null;
    for (const a of s.achievements) {
      if (!a.isEarned || !a.earnedAt) continue;
      if (!latest || Date.parse(a.earnedAt) > Date.parse(latest.earnedAt!)) latest = a;
    }
    return latest;
  });

  readonly earnedAchievements = computed(() =>
    this.sortItems((this.showcase()?.achievements ?? []).filter(a => a.isEarned)));

  readonly lockedAchievements = computed(() =>
    this.sortItems((this.showcase()?.achievements ?? []).filter(a => !a.isEarned)));

  private sortItems(items: AchievementShowcaseItem[]): AchievementShowcaseItem[] {
    const mode = this.sortBy();
    return [...items].sort((a, b) => {
      if (mode === 'name') return a.name.localeCompare(b.name);
      if (mode === 'recent') {
        const da = a.earnedAt ? Date.parse(a.earnedAt) : 0;
        const db = b.earnedAt ? Date.parse(b.earnedAt) : 0;
        if (db !== da) return db - da;
      }
      return b.tier - a.tier || a.name.localeCompare(b.name);
    });
  }

  ngOnInit(): void {
    this.playerId = this.route.snapshot.paramMap.get('playerId') || null;

    if (this.playerId) {
      this.api.getPlayerAchievementShowcase(this.playerId).subscribe({
        next: (data) => {
          this.showcase.set(data);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      this.api.getMyAchievementShowcase().subscribe({
        next: (data) => {
          this.showcase.set(data);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  starArray(n: number): number[] {
    return Array.from({ length: Math.max(0, n) }, (_, i) => i);
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString(this.transloco.getActiveLang(), {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  }

  goBack(): void {
    this.router.navigate(['/']);
  }
}
