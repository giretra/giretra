import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { LucideAngularModule, ChartSpline, Flame } from 'lucide-angular';
import { ApiService, HighlightsResponse } from '../../core/services/api.service';
import {
  HlActivityComponent,
  HlRadarComponent,
  HlTrendComponent,
} from './highlights-charts.component';
import {
  HlBiddingComponent,
  HlCalloutsComponent,
  HlPartnersComponent,
  HlSweepsComponent,
  HlTricksComponent,
} from './highlights-cards.component';

@Component({
  selector: 'app-highlights',
  standalone: true,
  imports: [
    LucideAngularModule,
    TranslocoDirective,
    HlRadarComponent,
    HlTrendComponent,
    HlActivityComponent,
    HlBiddingComponent,
    HlSweepsComponent,
    HlPartnersComponent,
    HlCalloutsComponent,
    HlTricksComponent,
  ],
  template: `
    <div class="hl-inner" *transloco="let t">
      <div class="page-head">
          <h1 class="header-title">
            <i-lucide [img]="ChartSplineIcon" [size]="18"></i-lucide>
            @if (viewedPlayerId && data()?.playerName; as name) {
              {{ name }}
            } @else {
              {{ t('highlights.title') }}
            }
          </h1>
      </div>

      @if (loading()) {
        <div class="loading-state">{{ t('common.loading') }}</div>
      } @else if (data(); as d) {
        @if (d.hero.gamesPlayed === 0) {
          <div class="empty-card">
            <i-lucide [img]="ChartSplineIcon" [size]="36"></i-lucide>
            <h2>{{ t('highlights.empty.title') }}</h2>
            <p>{{ t('highlights.empty.body') }}</p>
            <button class="cta-btn" (click)="goBack()">{{ t('highlights.empty.cta') }}</button>
          </div>
        } @else {
          <div class="hero">
            @if (d.hero.eloRating !== null) {
              <div class="tile">
                <span class="tile-label">{{ t('highlights.hero.elo') }}</span>
                <span class="tile-value">{{ d.hero.eloRating }}</span>
              </div>
            }
            <div class="tile">
              <span class="tile-label">{{ t('highlights.hero.winRate') }}</span>
              <span class="tile-value">{{ d.hero.winRate }}%</span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('highlights.hero.games') }}</span>
              <span class="tile-value">{{ d.hero.gamesPlayed }}</span>
              <span class="tile-sub">{{ t('highlights.hero.wins') }}: {{ d.hero.gamesWon }}</span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('highlights.hero.streak') }}</span>
              <span class="tile-value streak">
                {{ d.hero.winStreak }}
                @if (d.hero.winStreak >= 3) {
                  <i-lucide [img]="FlameIcon" [size]="18"></i-lucide>
                }
              </span>
              <span class="tile-sub">{{ t('highlights.hero.bestStreak') }}: {{ d.hero.bestWinStreak }}</span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('highlights.hero.form') }}</span>
              <span class="form-dots">
                @for (win of d.hero.recentForm; track $index) {
                  <span
                    class="dot"
                    [class.dot-win]="win"
                    [class.dot-loss]="!win"
                    [title]="win ? t('highlights.hero.win') : t('highlights.hero.loss')"
                  ></span>
                }
              </span>
            </div>
          </div>

          @if (d.eloTrend.length > 0) {
            <div class="grid2">
              <hl-radar [modeStats]="d.modeStats" />
              <hl-trend [points]="d.eloTrend" />
            </div>
          } @else {
            <hl-radar [modeStats]="d.modeStats" />
          }

          <div class="grid4">
            <hl-bidding [bidding]="d.bidding" />
            <hl-tricks [tricks]="d.tricks" />
            <hl-sweeps [sweeps]="d.sweeps" />
            <hl-partners [bestPartner]="d.bestPartner" [nemesis]="d.nemesis" />
          </div>

          <hl-callouts [callouts]="d.callouts" />
          <hl-activity [days]="d.activity" />
        }
      } @else {
        <div class="loading-state">{{ t('highlights.notFound') }}</div>
      }
    </div>
  `,
  styles: [
    `
    .header-title { display: flex; align-items: center; gap: 0.5rem; font-size: 1rem; font-weight: 600; color: hsl(var(--foreground)); margin: 0; }
    .header-title i-lucide { color: hsl(var(--gold)); }
    .hl-inner { display: flex; flex-direction: column; gap: 1rem; }
    .page-head { display:flex; align-items:center; gap:0.75rem; flex-wrap:wrap; }
    .loading-state { text-align: center; padding: 3rem 0; color: hsl(var(--muted-foreground)); }

    .empty-card { display: flex; flex-direction: column; align-items: center; gap: 0.75rem; text-align: center; padding: 3rem 1rem; background: hsl(var(--card)); border: 1px solid hsl(var(--border)); border-radius: var(--radius); color: hsl(var(--muted-foreground)); }
    .empty-card i-lucide { color: hsl(var(--gold)); }
    .empty-card h2 { margin: 0; color: hsl(var(--foreground)); font-size: 1.1rem; }
    .empty-card p { margin: 0; font-size: 0.9rem; }
    .cta-btn { margin-top: 0.5rem; padding: 0.5rem 1.25rem; border-radius: var(--radius); border: none; background: hsl(var(--primary)); color: hsl(var(--foreground)); font-weight: 600; cursor: pointer; transition: filter 0.15s ease; }
    .cta-btn:hover { filter: brightness(1.15); }

    .hero { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; }
    .tile { display: flex; flex-direction: column; gap: 0.2rem; background: hsl(var(--card)); border: 1px solid hsl(var(--border)); border-radius: var(--radius); padding: 0.85rem 1rem; }
    .tile-label { font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.05em; color: hsl(var(--muted-foreground)); }
    .tile-value { font-size: 1.5rem; font-weight: 700; color: hsl(var(--foreground)); font-variant-numeric: tabular-nums; display: flex; align-items: center; gap: 0.35rem; }
    .tile-value.streak i-lucide { color: hsl(var(--gold)); }
    .tile-sub { font-size: 0.72rem; color: hsl(var(--muted-foreground)); }
    .form-dots { display: flex; align-items: center; gap: 0.3rem; flex-wrap: wrap; min-height: 2.25rem; }
    .dot { width: 0.7rem; height: 0.7rem; border-radius: 50%; }
    .dot-win { background: hsl(var(--team2)); }
    .dot-loss { background: transparent; border: 2px solid hsl(var(--destructive)); }

    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .grid4 { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }

    @media (max-width: 1024px) {
      .hero { grid-template-columns: repeat(2, 1fr); }
      .grid2 { grid-template-columns: 1fr; }
      .grid4 { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 640px) {
      .grid4 { grid-template-columns: 1fr; }
    }
    @media (max-width: 480px) {
      .hero { grid-template-columns: 1fr; }
    }
  `,
  ],
})
export class HighlightsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly ChartSplineIcon = ChartSpline;
  readonly FlameIcon = Flame;

  readonly data = signal<HighlightsResponse | null>(null);
  readonly loading = signal<boolean>(true);

  viewedPlayerId: string | null = null;

  ngOnInit(): void {
    this.viewedPlayerId = this.route.snapshot.paramMap.get('playerId');
    const request = this.viewedPlayerId
      ? this.api.getPlayerHighlights(this.viewedPlayerId)
      : this.api.getMyHighlights();

    request.subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate(['/']);
  }
}
