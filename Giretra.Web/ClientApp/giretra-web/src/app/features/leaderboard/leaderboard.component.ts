import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService, LeaderboardEntryResponse } from '../../core/services/api.service';
import { LucideAngularModule, ChevronLeft, Trophy, Bot } from 'lucide-angular';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [LucideAngularModule],
  template: `
    <div class="lb-shell">
      <header class="lb-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()" title="Back to home">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="TrophyIcon" [size]="18"></i-lucide>
            Leaderboard
          </h1>
          @if (totalCount() > 0) {
            <span class="count-badge">{{ totalCount() }} rated players</span>
          }
        </div>
      </header>

      <main class="lb-main">
        <div class="lb-inner">
          @if (loading()) {
            <div class="loading-state">Loading...</div>
          } @else if (entries().length === 0) {
            <div class="empty-state">No players with 5+ rated games yet.</div>
          } @else {
            <!-- Players table -->
            <div class="row row-header">
              <div class="col-rank">#</div>
              <div class="col-player">Player</div>
              <div class="col-rating">Rating</div>
              <div class="col-games">Games</div>
              <div class="col-winrate">Win %</div>
            </div>

            @for (entry of playerEntries(); track entry.rank) {
              <div class="row" [class.row-top3]="entry.rank <= 3">
                <div class="col-rank">
                  @if (entry.rank === 1) {
                    <span class="medal medal-gold">1</span>
                  } @else if (entry.rank === 2) {
                    <span class="medal medal-silver">2</span>
                  } @else if (entry.rank === 3) {
                    <span class="medal medal-bronze">3</span>
                  } @else {
                    <span class="rank-num">{{ entry.rank }}</span>
                  }
                </div>
                <div class="col-player">
                  @if (entry.avatarUrl) {
                    <img class="avatar" [src]="entry.avatarUrl" [alt]="entry.displayName" />
                  } @else {
                    <span class="avatar avatar-placeholder">{{ entry.displayName.charAt(0).toUpperCase() }}</span>
                  }
                  <span class="player-name">{{ entry.displayName }}</span>
                </div>
                <div class="col-rating">{{ entry.rating }}</div>
                <div class="col-games">{{ entry.gamesPlayed }}</div>
                <div class="col-winrate">{{ entry.winRate }}%</div>
              </div>
            }

            <!-- Bots table -->
            @if (botEntries().length > 0) {
              <div class="section-divider">
                <div class="section-title">
                  <i-lucide [img]="BotIcon" [size]="14"></i-lucide>
                  Bots
                  <span class="count-badge count-badge-inline">{{ botEntries().length }}</span>
                </div>
              </div>

              <div class="row row-header">
                <div class="col-rank">#</div>
                <div class="col-player">Bot</div>
                <div class="col-rating">Rating</div>
                <div class="col-games">Games</div>
                <div class="col-winrate">Win %</div>
              </div>

              @for (entry of botEntries(); let i = $index; track entry.displayName) {
                <div class="row">
                  <div class="col-rank">
                    <span class="rank-num">{{ i + 1 }}</span>
                  </div>
                  <div class="col-player">
                    <span class="avatar avatar-placeholder avatar-bot">
                      <i-lucide [img]="BotIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                    </span>
                    <span class="player-name">{{ entry.displayName }}</span>
                  </div>
                  <div class="col-rating">{{ entry.rating }}</div>
                  <div class="col-games">{{ entry.gamesPlayed }}</div>
                  <div class="col-winrate">{{ entry.winRate }}%</div>
                </div>
              }
            }
          }
        </div>
      </main>
    </div>
  `,
  styles: [`
    .lb-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }

    /* Header */
    .lb-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:1rem; flex-shrink:0; }
    .header-inner { max-width:720px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { display:flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:0.5rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .count-badge { margin-left:auto; font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.625rem; border-radius:9999px; }

    /* Main */
    .lb-main { flex:1; padding:1rem; }
    .lb-inner { max-width:720px; margin:0 auto; display:flex; flex-direction:column; }

    .loading-state, .empty-state { text-align:center; padding:3rem 1rem; color:hsl(var(--muted-foreground)); font-size:0.875rem; }

    /* Row layout */
    .row { display:flex; align-items:center; padding:0.625rem 0.75rem; border-radius:0.5rem; gap:0.75rem; }
    .row-header { font-size:0.6875rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; color:hsl(var(--muted-foreground)); border-bottom:1px solid hsl(var(--border)); border-radius:0; margin-bottom:0.25rem; padding-bottom:0.5rem; }
    .row:not(.row-header):hover { background:hsl(var(--foreground)/0.03); }
    .row-top3 { background:hsl(var(--foreground)/0.02); }

    /* Columns */
    .col-rank { width:2.5rem; flex-shrink:0; text-align:center; }
    .col-player { flex:1; min-width:0; display:flex; align-items:center; gap:0.5rem; }
    .col-rating { width:4rem; flex-shrink:0; text-align:right; font-weight:700; font-size:0.875rem; color:hsl(var(--foreground)); font-variant-numeric:tabular-nums; }
    .col-games { width:3.5rem; flex-shrink:0; text-align:right; font-size:0.8125rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .col-winrate { width:3.5rem; flex-shrink:0; text-align:right; font-size:0.8125rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    /* Medal */
    .medal { display:inline-flex; align-items:center; justify-content:center; width:1.5rem; height:1.5rem; border-radius:50%; font-size:0.6875rem; font-weight:800; }
    .medal-gold { background:hsl(45 80% 50% / 0.2); color:hsl(45 90% 55%); border:1.5px solid hsl(45 80% 50% / 0.4); }
    .medal-silver { background:hsl(220 10% 60% / 0.2); color:hsl(220 10% 72%); border:1.5px solid hsl(220 10% 60% / 0.4); }
    .medal-bronze { background:hsl(25 60% 45% / 0.2); color:hsl(25 65% 55%); border:1.5px solid hsl(25 60% 45% / 0.4); }
    .rank-num { font-size:0.8125rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    /* Avatar */
    .avatar { width:1.75rem; height:1.75rem; border-radius:50%; flex-shrink:0; object-fit:cover; }
    .avatar-placeholder { display:inline-flex; align-items:center; justify-content:center; background:hsl(var(--muted)); color:hsl(var(--muted-foreground)); font-size:0.75rem; font-weight:700; }

    /* Player name */
    .player-name { font-size:0.875rem; font-weight:500; color:hsl(var(--foreground)); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }

    /* Bot avatar */
    .avatar-bot { background:hsl(var(--gold) / 0.15); color:hsl(var(--gold)); }

    /* Section divider */
    .section-divider { margin-top:1.5rem; margin-bottom:0.75rem; padding-top:1rem; border-top:1px solid hsl(var(--border)); }
    .section-title { display:flex; align-items:center; gap:0.375rem; font-size:0.8125rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.08em; }
    .count-badge-inline { margin-left:0.25rem; }

    /* Responsive: hide Games column on small screens */
    @media (max-width:480px) {
      .col-games { display:none; }
      .row { gap:0.5rem; padding:0.5rem; }
    }
  `],
})
export class LeaderboardComponent implements OnInit {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly TrophyIcon = Trophy;
  readonly BotIcon = Bot;

  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly entries = signal<LeaderboardEntryResponse[]>([]);
  readonly totalCount = signal<number>(0);
  readonly loading = signal<boolean>(true);

  readonly playerEntries = computed(() => this.entries().filter(e => !e.isBot));
  readonly botEntries = computed(() =>
    this.entries().filter(e => e.isBot).sort((a, b) => b.rating - a.rating)
  );

  ngOnInit(): void {
    this.api.getLeaderboard().subscribe({
      next: (res) => {
        this.entries.set(res.entries);
        this.totalCount.set(res.totalCount);
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
