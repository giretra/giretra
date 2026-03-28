import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import {
  ApiService,
  AchievementShowcaseResponse,
  AchievementShowcaseItem,
} from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { LucideAngularModule, ChevronLeft, Award, Star, Lock } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

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

            <!-- Info text -->
            <p class="ach-info-text">{{ t('achievements.page.info') }}</p>

            <!-- Progress bar -->
            <div class="progress-section">
              <div class="progress-bar-track">
                <div class="progress-bar-fill" [style.width.%]="progressPercent()"></div>
              </div>
              <div class="progress-stats">
                <span class="progress-earned">{{ showcase()!.earnedCount }} {{ t('achievements.page.earned') }}</span>
                <span class="progress-locked">{{ showcase()!.totalCount - showcase()!.earnedCount }} {{ t('achievements.page.locked') }}</span>
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
                      class="ach-card"
                      [class.earned]="ach.isEarned"
                      [class.locked]="!ach.isEarned"
                      [class.hidden-ach]="ach.isHidden && !ach.isEarned"
                      [class.tier-high]="ach.tier >= 4"
                    >
                      <div class="ach-card-top">
                        <div class="ach-card-icon" [class.earned]="ach.isEarned">
                          @if (ach.isEarned) {
                            <i-lucide [img]="AwardIcon" [size]="20" [strokeWidth]="1.5"></i-lucide>
                          } @else {
                            <i-lucide [img]="LockIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                          }
                        </div>
                        <div class="ach-card-info">
                          @if (ach.isHidden && !ach.isEarned) {
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
                        @if (ach.isEarned) {
                          <span class="earned-badge">{{ t('achievements.page.earned') }}</span>
                        }
                      </div>

                      <div class="ach-card-details">
                        @if (ach.isHidden && !ach.isEarned) {
                          <p class="ach-card-desc hidden-text">{{ t('achievements.page.hiddenDesc') }}</p>
                        } @else {
                          <p class="ach-card-desc">{{ t('achievements.desc.' + ach.code) }}</p>
                        }
                        @if (ach.isEarned && ach.earnedAt) {
                          <span class="ach-card-date">{{ t('achievements.page.earnedOn', { date: formatDate(ach.earnedAt) }) }}</span>
                        }
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
    :host {
      display: block;
      height: 100%;
    }

    .ach-shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
      min-height: 100dvh;
      background: hsl(var(--background));
      color: hsl(var(--foreground));
    }

    /* Header */
    .ach-header {
      background: hsl(var(--card));
      border-bottom: 1px solid hsl(var(--border));
      padding: 0.75rem 1rem;
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .header-inner {
      max-width: 640px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .back-btn {
      background: none;
      border: none;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      padding: 0.375rem;
      border-radius: 0.5rem;
      display: grid;
      place-items: center;
      transition: color 0.15s ease, background 0.15s ease;
    }

    .back-btn:hover {
      color: hsl(var(--foreground));
      background: hsl(var(--muted) / 0.5);
    }

    .header-title {
      font-size: 1.125rem;
      font-weight: 700;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: hsl(var(--gold));
    }

    .header-subtitle {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      margin-left: auto;
      font-variant-numeric: tabular-nums;
    }

    /* Main */
    .ach-main {
      flex: 1;
      overflow-y: auto;
      padding: 1rem;
    }

    .ach-inner {
      max-width: 640px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    /* Loading */
    .loading-state {
      display: flex;
      justify-content: center;
      padding: 3rem;
    }

    .loading-spinner {
      width: 2rem;
      height: 2rem;
      border: 2px solid hsl(var(--muted));
      border-top-color: hsl(var(--gold));
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    /* Player banner */
    .player-banner {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
    }

    .player-avatar {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      border: 2px solid hsl(var(--gold) / 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      text-transform: uppercase;
    }

    .player-name {
      font-size: 1rem;
      font-weight: 600;
      color: hsl(var(--foreground));
    }

    /* Info text */
    .ach-info-text {
      font-size: 0.8125rem;
      color: hsl(var(--muted-foreground));
      line-height: 1.5;
      margin: 0;
      padding: 0.625rem 0.75rem;
      background: hsl(var(--secondary) / 0.5);
      border: 1px solid hsl(var(--border) / 0.5);
      border-radius: 0.625rem;
    }

    /* Progress */
    .progress-section {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .progress-bar-track {
      height: 0.5rem;
      background: hsl(var(--muted));
      border-radius: 9999px;
      overflow: hidden;
    }

    .progress-bar-fill {
      height: 100%;
      background: linear-gradient(90deg, hsl(var(--gold)), hsl(45 95% 65%));
      border-radius: 9999px;
      transition: width 0.6s cubic-bezier(0.2, 0, 0, 1);
      box-shadow: 0 0 8px hsl(var(--gold) / 0.4);
    }

    .progress-stats {
      display: flex;
      justify-content: space-between;
      font-size: 0.6875rem;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .progress-earned {
      color: hsl(var(--gold));
    }

    .progress-locked {
      color: hsl(var(--muted-foreground));
    }

    /* Section headers */
    .section-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      padding-bottom: 0.25rem;
    }

    .earned-section-header {
      color: hsl(var(--gold));
    }

    .locked-section-header {
      color: hsl(var(--muted-foreground));
    }

    .section-count {
      margin-left: auto;
      font-size: 0.75rem;
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }

    /* Achievement grid */
    .tier-grid {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    /* Achievement card */
    .ach-card {
      display: flex;
      flex-direction: column;
      padding: 0.75rem;
      border-radius: 0.75rem;
      border: 1px solid hsl(var(--border) / 0.6);
      background: hsl(var(--card));
      transition: all 0.15s ease;
      animation: cardIn 0.3s ease both;
    }

    .ach-card:hover {
      background: hsl(var(--secondary) / 0.8);
    }

    .ach-card.earned {
      border-color: hsl(var(--gold) / 0.3);
      background: hsl(var(--gold) / 0.04);
    }

    .ach-card.earned:hover {
      background: hsl(var(--gold) / 0.08);
    }

    .ach-card.earned.tier-high {
      border-color: hsl(var(--gold) / 0.5);
      background: hsl(var(--gold) / 0.08);
      box-shadow: 0 0 16px hsl(var(--gold) / 0.08);
    }

    .ach-card.locked {
      opacity: 0.6;
    }

    .ach-card.locked:hover {
      opacity: 0.8;
    }

    .ach-card.hidden-ach {
      border-style: dashed;
    }

    @keyframes cardIn {
      from { opacity: 0; transform: translateY(4px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .ach-card-top {
      display: flex;
      align-items: center;
      gap: 0.625rem;
    }

    .ach-card-icon {
      width: 2.25rem;
      height: 2.25rem;
      border-radius: 0.625rem;
      display: grid;
      place-items: center;
      flex-shrink: 0;
      background: hsl(var(--muted));
      color: hsl(var(--muted-foreground));
    }

    .ach-card-icon.earned {
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
    }

    .ach-card-info {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
    }

    .ach-card-name {
      font-size: 0.9375rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .ach-card-name.hidden-text {
      color: hsl(var(--muted-foreground));
      font-style: italic;
    }

    .ach-card-stars {
      display: flex;
      gap: 1px;
    }

    .star {
      font-size: 0.75rem;
      line-height: 1;
    }

    .star.filled {
      color: hsl(var(--gold));
    }

    .star.empty {
      color: hsl(var(--muted) / 0.5);
    }

    .earned-badge {
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
      flex-shrink: 0;
      white-space: nowrap;
    }

    /* Expanded details */
    .ach-card-details {
      margin-top: 0.625rem;
      padding-top: 0.5rem;
      border-top: 1px solid hsl(var(--border) / 0.4);
    }

    .ach-card-desc {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      line-height: 1.5;
      margin: 0;
    }

    .ach-card-desc.hidden-text {
      font-style: italic;
    }

    .ach-card-date {
      display: block;
      font-size: 0.75rem;
      color: hsl(var(--gold) / 0.8);
      margin-top: 0.375rem;
    }

    /* Empty state */
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      padding: 3rem 1rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .empty-state p {
      font-size: 0.875rem;
      margin: 0;
    }

    /* Mobile */
    @media (max-width: 480px) {
      .ach-header {
        padding: 0.625rem 0.75rem;
      }

      .header-title {
        font-size: 1rem;
      }

      .ach-main {
        padding: 0.75rem;
      }

      .ach-card {
        padding: 0.625rem;
      }

      .ach-card-icon {
        width: 2rem;
        height: 2rem;
        border-radius: 0.5rem;
      }

      .ach-card-name {
        font-size: 0.875rem;
      }

      .earned-badge {
        font-size: 0.625rem;
        padding: 0.0625rem 0.375rem;
      }
    }
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

  readonly loading = signal(true);
  readonly showcase = signal<AchievementShowcaseResponse | null>(null);
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

  readonly earnedAchievements = computed(() => {
    const s = this.showcase();
    if (!s) return [];
    return s.achievements
      .filter(a => a.isEarned)
      .sort((a, b) => b.tier - a.tier || a.name.localeCompare(b.name));
  });

  readonly lockedAchievements = computed(() => {
    const s = this.showcase();
    if (!s) return [];
    return s.achievements
      .filter(a => !a.isEarned)
      .sort((a, b) => b.tier - a.tier || a.name.localeCompare(b.name));
  });

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
