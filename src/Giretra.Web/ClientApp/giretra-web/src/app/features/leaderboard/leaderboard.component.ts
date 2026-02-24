import { Component, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  ApiService,
  LeaderboardPlayerEntry,
  LeaderboardBotEntry,
  PlayerProfileResponse,
} from '../../core/services/api.service';
import { LucideAngularModule, ChevronLeft, Trophy, Bot, Users } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { PlayerProfilePopupComponent } from '../../shared/components/player-profile-popup/player-profile-popup.component';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, PlayerProfilePopupComponent],
  template: `
    <div class="lb-shell" *transloco="let t">
      <header class="lb-header">
        <div class="header-inner">
          <button class="back-btn" (click)="goBack()" title="Back to home">
            <i-lucide [img]="ChevronLeftIcon" [size]="18" [strokeWidth]="2"></i-lucide>
          </button>
          <h1 class="header-title">
            <i-lucide [img]="TrophyIcon" [size]="18"></i-lucide>
            {{ t('leaderboard.title') }}
          </h1>
          <div class="header-badges">
            @if (playerCount() > 0) {
              <span class="count-badge">{{ t('leaderboard.playerCount', { count: playerCount() }) }}</span>
            }
            @if (botCount() > 0) {
              <span class="count-badge count-badge-bot">{{ t('leaderboard.botCount', { count: botCount() }) }}</span>
            }
          </div>
        </div>
      </header>

      <main class="lb-main">
        <div class="lb-inner">
          @if (loading()) {
            <div class="loading-state">{{ t('common.loading') }}</div>
          } @else {
            <div class="columns">

              <!-- Players Column -->
              <section class="column">
                <div class="column-head">
                  <i-lucide [img]="UsersIcon" [size]="14"></i-lucide>
                  <span class="column-label">{{ t('leaderboard.players') }}</span>
                  <span class="column-count">{{ playerCount() }}</span>
                </div>

                @if (players().length === 0) {
                  <div class="empty-col">{{ t('leaderboard.noPlayers') }}</div>
                } @else {
                  <div class="row row-header">
                    <div class="col-rank">{{ t('leaderboard.columns.rank') }}</div>
                    <div class="col-name">{{ t('leaderboard.columns.player') }}</div>
                    <div class="col-rating">{{ t('leaderboard.columns.rating') }}</div>
                    <div class="col-games">{{ t('leaderboard.columns.games') }}</div>
                    <div class="col-winrate">{{ t('leaderboard.columns.winRate') }}</div>
                  </div>

                  @for (p of players(); track p.rank) {
                    <div class="row row-clickable" [class.row-top3]="p.rank <= 3" (click)="openProfile(p.playerId)">
                      <div class="col-rank">
                        @if (p.rank === 1) {
                          <span class="medal medal-gold">1</span>
                        } @else if (p.rank === 2) {
                          <span class="medal medal-silver">2</span>
                        } @else if (p.rank === 3) {
                          <span class="medal medal-bronze">3</span>
                        } @else {
                          <span class="rank-num">{{ p.rank }}</span>
                        }
                      </div>
                      <div class="col-name">
                        @if (p.avatarUrl) {
                          <img class="avatar" [src]="p.avatarUrl" [alt]="p.displayName" />
                        } @else {
                          <span class="avatar avatar-placeholder">{{ p.displayName.charAt(0).toUpperCase() }}</span>
                        }
                        <span class="entry-name">{{ p.displayName }}</span>
                      </div>
                      <div class="col-rating">{{ p.rating }}</div>
                      <div class="col-games">{{ p.gamesPlayed }}</div>
                      <div class="col-winrate">{{ p.winRate }}%</div>
                    </div>
                  }
                }
              </section>

              <!-- Bots Column -->
              <section class="column column-bots">
                <div class="column-head column-head-bot">
                  <i-lucide [img]="BotIcon" [size]="14"></i-lucide>
                  <span class="column-label">{{ t('leaderboard.bots') }}</span>
                  <span class="column-count">{{ botCount() }}</span>
                </div>

                @if (bots().length === 0) {
                  <div class="empty-col">{{ t('leaderboard.noBots') }}</div>
                } @else {
                  <div class="row row-header">
                    <div class="col-rank">{{ t('leaderboard.columns.rank') }}</div>
                    <div class="col-name">{{ t('leaderboard.columns.bot') }}</div>
                    <div class="col-rating">{{ t('leaderboard.columns.rating') }}</div>
                    <div class="col-games">{{ t('leaderboard.columns.games') }}</div>
                    <div class="col-winrate">{{ t('leaderboard.columns.winRate') }}</div>
                  </div>

                  @for (b of bots(); track b.rank) {
                    <div class="row row-clickable" [class.row-top3]="b.rank <= 3" (click)="openProfile(b.playerId)">
                      <div class="col-rank">
                        @if (b.rank === 1) {
                          <span class="medal medal-gold">1</span>
                        } @else if (b.rank === 2) {
                          <span class="medal medal-silver">2</span>
                        } @else if (b.rank === 3) {
                          <span class="medal medal-bronze">3</span>
                        } @else {
                          <span class="rank-num">{{ b.rank }}</span>
                        }
                      </div>
                      <div class="col-name">
                        <span class="avatar avatar-placeholder avatar-bot">
                          <i-lucide [img]="BotIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                        </span>
                        <div class="name-group">
                          <span class="entry-name">{{ b.displayName }}</span>
                          @if (b.author) {
                            <span class="bot-author">by {{ b.author }}</span>
                          }
                        </div>
                      </div>
                      <div class="col-rating">{{ b.rating }}</div>
                      <div class="col-games">{{ b.gamesPlayed }}</div>
                      <div class="col-winrate">{{ b.winRate }}%</div>
                    </div>
                  }
                }
              </section>

            </div>
          }
        </div>
      </main>
    </div>

    @if (profileData()) {
      <app-player-profile-popup
        [profile]="profileData()!"
        (closed)="closeProfile()"
      />
    }
  `,
  styles: [`
    .lb-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }

    /* Header */
    .lb-header { background:hsl(var(--card)); border-bottom:1px solid hsl(var(--border)); padding:1rem; flex-shrink:0; }
    .header-inner { max-width:960px; margin:0 auto; display:flex; align-items:center; gap:0.75rem; }
    .back-btn { display:flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:0.5rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .back-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .header-badges { margin-left:auto; display:flex; gap:0.375rem; }
    .count-badge { font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.625rem; border-radius:9999px; }
    .count-badge-bot { color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); }

    /* Main */
    .lb-main { flex:1; padding:1rem; }
    .lb-inner { max-width:960px; margin:0 auto; }

    .loading-state { text-align:center; padding:3rem 1rem; color:hsl(var(--muted-foreground)); font-size:0.875rem; }

    /* Columns */
    .columns { display:grid; grid-template-columns:1fr 1fr; gap:1rem; align-items:start; }

    .column { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:1rem; display:flex; flex-direction:column; }

    /* Column header */
    .column-head { display:flex; align-items:center; gap:0.375rem; margin-bottom:0.75rem; padding-bottom:0.625rem; border-bottom:1px solid hsl(var(--border)); color:hsl(var(--muted-foreground)); }
    .column-head-bot { color:hsl(var(--gold)); }
    .column-label { font-size:0.8125rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; }
    .column-count { margin-left:auto; font-size:0.625rem; font-weight:600; background:hsl(var(--foreground)/0.06); padding:0.0625rem 0.4375rem; border-radius:9999px; }

    .empty-col { text-align:center; padding:2rem 0.5rem; color:hsl(var(--muted-foreground)); font-size:0.8125rem; }

    /* Row layout */
    .row { display:flex; align-items:center; padding:0.5rem 0.5rem; border-radius:0.375rem; gap:0.5rem; }
    .row-header { font-size:0.625rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; color:hsl(var(--muted-foreground)); border-radius:0; margin-bottom:0.125rem; padding-bottom:0.375rem; }
    .row:not(.row-header):hover { background:hsl(var(--foreground)/0.03); }
    .row-top3 { background:hsl(var(--foreground)/0.02); }
    .row-clickable { cursor:pointer; }

    /* Columns */
    .col-rank { width:2rem; flex-shrink:0; text-align:center; }
    .col-name { flex:1; min-width:0; display:flex; align-items:center; gap:0.375rem; }
    .col-rating { width:3.25rem; flex-shrink:0; text-align:right; font-weight:700; font-size:0.8125rem; color:hsl(var(--foreground)); font-variant-numeric:tabular-nums; }
    .col-games { width:2.75rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .col-winrate { width:3rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    /* Medal */
    .medal { display:inline-flex; align-items:center; justify-content:center; width:1.375rem; height:1.375rem; border-radius:50%; font-size:0.625rem; font-weight:800; }
    .medal-gold { background:hsl(45 80% 50% / 0.2); color:hsl(45 90% 55%); border:1.5px solid hsl(45 80% 50% / 0.4); }
    .medal-silver { background:hsl(220 10% 60% / 0.2); color:hsl(220 10% 72%); border:1.5px solid hsl(220 10% 60% / 0.4); }
    .medal-bronze { background:hsl(25 60% 45% / 0.2); color:hsl(25 65% 55%); border:1.5px solid hsl(25 60% 45% / 0.4); }
    .rank-num { font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    /* Avatar */
    .avatar { width:1.5rem; height:1.5rem; border-radius:50%; flex-shrink:0; object-fit:cover; }
    .avatar-placeholder { display:inline-flex; align-items:center; justify-content:center; background:hsl(var(--muted)); color:hsl(var(--muted-foreground)); font-size:0.6875rem; font-weight:700; }
    .avatar-bot { background:hsl(var(--gold) / 0.15); color:hsl(var(--gold)); }

    /* Name */
    .entry-name { font-size:0.8125rem; font-weight:500; color:hsl(var(--foreground)); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .name-group { display:flex; flex-direction:column; gap:0; min-width:0; }
    .bot-author { font-size:0.625rem; color:hsl(var(--muted-foreground)); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; line-height:1.2; }

    /* Responsive: stack columns on narrow screens */
    @media (max-width:640px) {
      .columns { grid-template-columns:1fr; }
    }

    /* Responsive: hide Games column on small screens */
    @media (max-width:480px) {
      .col-games { display:none; }
      .row { gap:0.375rem; padding:0.375rem 0.375rem; }
    }
  `],
})
export class LeaderboardComponent implements OnInit {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly TrophyIcon = Trophy;
  readonly BotIcon = Bot;
  readonly UsersIcon = Users;

  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly players = signal<LeaderboardPlayerEntry[]>([]);
  readonly bots = signal<LeaderboardBotEntry[]>([]);
  readonly playerCount = signal<number>(0);
  readonly botCount = signal<number>(0);
  readonly loading = signal<boolean>(true);
  readonly profileData = signal<PlayerProfileResponse | null>(null);

  ngOnInit(): void {
    this.api.getLeaderboard().subscribe({
      next: (res) => {
        this.players.set(res.players);
        this.bots.set(res.bots);
        this.playerCount.set(res.playerCount);
        this.botCount.set(res.botCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  openProfile(playerId: string): void {
    this.api.getLeaderboardProfile(playerId).subscribe({
      next: (profile) => this.profileData.set(profile),
    });
  }

  closeProfile(): void {
    this.profileData.set(null);
  }

  goBack(): void {
    this.router.navigate(['/']);
  }
}
