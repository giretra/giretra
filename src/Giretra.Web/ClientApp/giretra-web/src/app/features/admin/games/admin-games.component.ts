import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService, AdminGameEntry, AdminGamePlayerEntry, AdminDealEntry } from '../../../core/services/api.service';
import { PlayerPosition } from '../../../api/generated/signalr-types.generated';
import { LucideAngularModule, Dices, X, Trophy, Check, Zap } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { GameModeBadgeComponent } from '../../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../../shared/components/multiplier-badge/multiplier-badge.component';

@Component({
  selector: 'app-admin-games',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, DatePipe, GameModeBadgeComponent, MultiplierBadgeComponent],
  template: `
    <div class="ag-inner" *transloco="let t">
      <div class="page-head">
          <h1 class="header-title">
            <i-lucide [img]="DicesIcon" [size]="18"></i-lucide>
            {{ t('adminGames.title') }}
          </h1>
          @if (totalCount() > 0) {
            <span class="count-badge">{{ t('adminGames.gameCount', { count: totalCount() }) }}</span>
          }
      </div>

      @if (filterUserId()) {
        <div class="filter-banner">
          <span class="filter-text">{{ t('adminGames.filteredBy', { name: filterUserName() || filterUserId() }) }}</span>
          <button class="filter-clear" (click)="clearFilter()" [title]="t('adminGames.clearFilter')">
            <i-lucide [img]="XIcon" [size]="14" [strokeWidth]="2"></i-lucide>
          </button>
        </div>
      }

      @if (loading()) {
        <div class="loading-state">{{ t('common.loading') }}</div>
      } @else if (games().length === 0) {
        <div class="empty-state">{{ t('adminGames.noGames') }}</div>
      } @else {
        <div class="table-panel">
          <div class="row row-header">
            <div class="col-room">{{ t('adminGames.columns.room') }}</div>
            <div class="col-team">{{ t('teams.team1') }}</div>
            <div class="col-score">{{ t('adminGames.columns.score') }}</div>
            <div class="col-team">{{ t('teams.team2') }}</div>
            <div class="col-deals">{{ t('adminGames.columns.deals') }}</div>
            <div class="col-flags"></div>
            <div class="col-date">{{ t('adminGames.columns.date') }}</div>
          </div>

          @for (g of games(); track g.id) {
            <div class="row row-clickable" (click)="openDetail(g)">
              <div class="col-room">{{ g.roomName }}</div>
              <div class="col-team" [class.team-winner]="g.winnerTeam === 'Team1'">
                @if (g.winnerTeam === 'Team1') {
                  <i-lucide [img]="TrophyIcon" [size]="11" class="win-icon"></i-lucide>
                }
                <span class="team-names">
                  @for (p of teamPlayers(g, 'Team1'); track p.position; let last = $last) {
                    <span
                      class="player-name"
                      [class.player-link]="!p.isBot && p.userId"
                      (click)="filterByPlayer(p, $event)"
                    >{{ p.displayName }}</span>{{ last ? '' : ' & ' }}
                  }
                </span>
              </div>
              <div class="col-score">
                <span [class.score-win]="g.winnerTeam === 'Team1'">{{ g.team1FinalScore }}</span>
                <span class="score-sep">–</span>
                <span [class.score-win]="g.winnerTeam === 'Team2'">{{ g.team2FinalScore }}</span>
              </div>
              <div class="col-team" [class.team-winner]="g.winnerTeam === 'Team2'">
                @if (g.winnerTeam === 'Team2') {
                  <i-lucide [img]="TrophyIcon" [size]="11" class="win-icon"></i-lucide>
                }
                <span class="team-names">
                  @for (p of teamPlayers(g, 'Team2'); track p.position; let last = $last) {
                    <span
                      class="player-name"
                      [class.player-link]="!p.isBot && p.userId"
                      (click)="filterByPlayer(p, $event)"
                    >{{ p.displayName }}</span>{{ last ? '' : ' & ' }}
                  }
                </span>
              </div>
              <div class="col-deals">{{ g.totalDeals }}</div>
              <div class="col-flags">
                @if (g.isRanked) {
                  <span class="flag flag-ranked">{{ t('adminGames.ranked') }}</span>
                }
                @if (g.wasAbandoned) {
                  <span class="flag flag-abandoned">{{ t('adminGames.abandoned') }}</span>
                } @else if (!g.completedAt) {
                  <span class="flag flag-live">{{ t('adminGames.inProgress') }}</span>
                }
              </div>
              <div class="col-date" [title]="g.startedAt">{{ g.startedAt | date: 'MMM d, y HH:mm' }}</div>
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

      <!-- Game detail panel -->
      @if (selectedGame(); as g) {
        <div class="detail-backdrop" (click)="closeDetail()">
      <div class="detail-panel" (click)="$event.stopPropagation()">
        <div class="detail-head">
          <div class="detail-title-group">
            <h2 class="detail-title">{{ g.roomName }}</h2>
            <span class="detail-date">{{ g.startedAt | date: 'MMM d, y HH:mm' }}</span>
          </div>
          <div class="detail-flags">
            @if (g.isRanked) {
              <span class="flag flag-ranked">{{ t('adminGames.ranked') }}</span>
            }
            @if (g.wasAbandoned) {
              <span class="flag flag-abandoned">{{ t('adminGames.abandoned') }}</span>
            } @else if (!g.completedAt) {
              <span class="flag flag-live">{{ t('adminGames.inProgress') }}</span>
            }
          </div>
          <button class="detail-close" (click)="closeDetail()">
            <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
          </button>
        </div>

        <div class="detail-scoreline">
          <span class="scoreline-team" [class.scoreline-winner]="g.winnerTeam === 'Team1'">
            {{ teamLabel(g, 'Team1') }}
          </span>
          <span class="scoreline-score">
            <span [class.score-win]="g.winnerTeam === 'Team1'">{{ g.team1FinalScore }}</span>
            <span class="score-sep">–</span>
            <span [class.score-win]="g.winnerTeam === 'Team2'">{{ g.team2FinalScore }}</span>
          </span>
          <span class="scoreline-team scoreline-right" [class.scoreline-winner]="g.winnerTeam === 'Team2'">
            {{ teamLabel(g, 'Team2') }}
          </span>
        </div>

        @if (dealsLoading()) {
          <div class="detail-loading">{{ t('common.loading') }}</div>
        } @else if (deals().length === 0) {
          <div class="detail-loading">{{ t('adminGames.detail.noDeals') }}</div>
        } @else {
          <div class="deals-table">
            <div class="deal-row deal-row-header">
              <div class="dcol-num">#</div>
              <div class="dcol-mode">{{ t('adminGames.detail.mode') }}</div>
              <div class="dcol-announcer">{{ t('adminGames.detail.announcer') }}</div>
              <div class="dcol-dealer">{{ t('adminGames.detail.dealer') }}</div>
              <div class="dcol-pts">{{ t('adminGames.detail.cardPoints') }}</div>
              <div class="dcol-pts">{{ t('adminGames.detail.matchPoints') }}</div>
              <div class="dcol-notes"></div>
            </div>
            @for (d of deals(); track d.dealNumber) {
              <div class="deal-row">
                <div class="dcol-num">{{ d.dealNumber }}</div>
                <div class="dcol-mode">
                  @if (d.gameMode) {
                    <app-game-mode-badge [mode]="d.gameMode" size="0.875rem" [compact]="isColourMode(d.gameMode)" />
                  } @else {
                    <span class="dcol-empty">–</span>
                  }
                  <app-multiplier-badge [multiplier]="d.multiplier" />
                </div>
                <div class="dcol-announcer">
                  @if (d.announcerTeam) {
                    <span
                      class="announcer-chip"
                      [class.announcer-won]="d.announcerWon === true"
                      [class.announcer-lost]="d.announcerWon === false"
                      [title]="announcerNames(g, d)"
                    >{{ d.announcerTeam === 'Team1' ? t('teams.team1') : t('teams.team2') }}</span>
                  } @else {
                    <span class="dcol-empty">–</span>
                  }
                </div>
                <div class="dcol-dealer">{{ playerAt(g, d.dealerPosition) }}</div>
                <div class="dcol-pts">
                  @if (d.team1CardPoints !== null || d.team2CardPoints !== null) {
                    <span [class.pts-lead]="(d.team1CardPoints ?? 0) > (d.team2CardPoints ?? 0)">{{ d.team1CardPoints ?? 0 }}</span>
                    <span class="score-sep">–</span>
                    <span [class.pts-lead]="(d.team2CardPoints ?? 0) > (d.team1CardPoints ?? 0)">{{ d.team2CardPoints ?? 0 }}</span>
                  } @else {
                    <span class="dcol-empty">–</span>
                  }
                </div>
                <div class="dcol-pts">
                  @if (d.team1MatchPoints !== null || d.team2MatchPoints !== null) {
                    <span [class.pts-lead]="(d.team1MatchPoints ?? 0) > (d.team2MatchPoints ?? 0)">{{ d.team1MatchPoints ?? 0 }}</span>
                    <span class="score-sep">–</span>
                    <span [class.pts-lead]="(d.team2MatchPoints ?? 0) > (d.team1MatchPoints ?? 0)">{{ d.team2MatchPoints ?? 0 }}</span>
                  } @else {
                    <span class="dcol-empty">–</span>
                  }
                </div>
                <div class="dcol-notes">
                  @if (d.wasSweep) {
                    <span class="flag flag-sweep" [title]="d.sweepingTeam === 'Team1' ? t('teams.team1') : t('teams.team2')">
                      <i-lucide [img]="CheckIcon" [size]="10" [strokeWidth]="3"></i-lucide>
                      {{ t('adminGames.detail.sweep') }}
                    </span>
                  }
                  @if (d.isInstantWin) {
                    <span class="flag flag-instant">
                      <i-lucide [img]="ZapIcon" [size]="10" [strokeWidth]="2.5"></i-lucide>
                      {{ t('adminGames.detail.instantWin') }}
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        }
      </div>
        </div>
      }
    </div>
  `,
  styles: [`

    /* Header */
    .header-title { margin:0; font-size:1.125rem; font-weight:700; color:hsl(var(--foreground)); display:flex; align-items:center; gap:0.5rem; }
    .count-badge { margin-left:auto; font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.625rem; border-radius:9999px; }

    /* Main */
    .ag-inner { }
    .page-head { display:flex; align-items:center; gap:0.75rem; flex-wrap:wrap; margin-bottom:1rem; }

    .loading-state, .empty-state { text-align:center; padding:3rem 1rem; color:hsl(var(--muted-foreground)); font-size:0.875rem; }

    /* Filter banner */
    .filter-banner { display:flex; align-items:center; gap:0.5rem; background:hsl(var(--card)); border:1px solid hsl(var(--gold)/0.3); border-radius:0.75rem; padding:0.5rem 1rem; margin-bottom:1rem; }
    .filter-text { font-size:0.8125rem; color:hsl(var(--foreground)); }
    .filter-clear { display:flex; align-items:center; justify-content:center; width:1.5rem; height:1.5rem; margin-left:auto; border-radius:0.375rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; }
    .filter-clear:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.08); }

    /* Table */
    .table-panel { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:0.75rem 1rem; overflow-x:auto; }
    .row { display:flex; align-items:center; padding:0.5rem 0.25rem; gap:0.625rem; min-width:56rem; }
    .row:not(.row-header) { border-top:1px solid hsl(var(--border)/0.5); }
    .row-header { font-size:0.625rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; color:hsl(var(--muted-foreground)); }

    .col-room { width:9rem; flex-shrink:0; font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .col-team { flex:1; min-width:9rem; display:flex; align-items:center; gap:0.25rem; font-size:0.75rem; color:hsl(var(--muted-foreground)); overflow:hidden; }
    .team-winner { color:hsl(var(--foreground)); }
    .win-icon { color:hsl(var(--gold)); flex-shrink:0; }
    .team-names { overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .player-link { cursor:pointer; text-decoration:underline; text-decoration-color:hsl(var(--muted-foreground)/0.4); text-underline-offset:2px; }
    .player-link:hover { color:hsl(var(--gold)); }
    .col-score { width:5rem; flex-shrink:0; text-align:center; font-size:0.8125rem; font-weight:700; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .score-win { color:hsl(var(--foreground)); }
    .score-sep { margin:0 0.125rem; font-weight:400; }
    .col-deals { width:3rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .col-flags { width:8rem; flex-shrink:0; display:flex; gap:0.25rem; justify-content:flex-end; }
    .flag { font-size:0.5625rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; padding:0.0625rem 0.4375rem; border-radius:9999px; }
    .flag-ranked { color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); }
    .flag-abandoned { color:hsl(0 70% 55%); background:hsl(0 70% 50% / 0.12); }
    .flag-live { color:hsl(140 60% 45%); background:hsl(140 60% 45% / 0.12); }
    .col-date { width:8.5rem; flex-shrink:0; text-align:right; font-size:0.75rem; color:hsl(var(--muted-foreground)); white-space:nowrap; }

    .row-clickable { cursor:pointer; }
    .row-clickable:hover { background:hsl(var(--foreground)/0.03); }

    /* Detail panel */
    .detail-backdrop { position:fixed; inset:0; background:hsl(0 0% 0% / 0.5); display:flex; align-items:center; justify-content:center; z-index:50; padding:1rem; }
    .detail-panel { background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; padding:1.25rem; width:100%; max-width:44rem; max-height:85vh; overflow-y:auto; display:flex; flex-direction:column; gap:0.875rem; }
    .detail-head { display:flex; align-items:flex-start; gap:0.75rem; }
    .detail-title-group { display:flex; flex-direction:column; gap:0.125rem; min-width:0; }
    .detail-title { margin:0; font-size:1rem; font-weight:700; color:hsl(var(--foreground)); overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .detail-date { font-size:0.6875rem; color:hsl(var(--muted-foreground)); }
    .detail-flags { display:flex; gap:0.25rem; margin-left:auto; flex-shrink:0; padding-top:0.125rem; }
    .detail-close { display:flex; align-items:center; justify-content:center; width:1.75rem; height:1.75rem; border-radius:0.375rem; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; flex-shrink:0; }
    .detail-close:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.08); }

    .detail-scoreline { display:flex; align-items:center; gap:0.75rem; background:hsl(var(--background)); border:1px solid hsl(var(--border)); border-radius:0.625rem; padding:0.625rem 0.875rem; }
    .scoreline-team { flex:1; font-size:0.75rem; color:hsl(var(--muted-foreground)); min-width:0; overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .scoreline-right { text-align:right; }
    .scoreline-winner { color:hsl(var(--foreground)); font-weight:600; }
    .scoreline-score { font-size:1rem; font-weight:800; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; flex-shrink:0; }

    .detail-loading { text-align:center; padding:2rem 1rem; color:hsl(var(--muted-foreground)); font-size:0.8125rem; }

    /* Deals table */
    .deals-table { display:flex; flex-direction:column; overflow-x:auto; }
    .deal-row { display:flex; align-items:center; gap:0.5rem; padding:0.4375rem 0.25rem; min-width:36rem; }
    .deal-row:not(.deal-row-header) { border-top:1px solid hsl(var(--border)/0.5); }
    .deal-row-header { font-size:0.625rem; font-weight:600; text-transform:uppercase; letter-spacing:0.08em; color:hsl(var(--muted-foreground)); }
    .dcol-num { width:1.5rem; flex-shrink:0; text-align:center; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .dcol-mode { width:8.5rem; flex-shrink:0; display:flex; align-items:center; gap:0.375rem; font-size:0.6875rem; }
    .dcol-mode ::ng-deep .badge { padding:0.125rem 0.375rem; }
    .dcol-mode ::ng-deep .mode-text { font-size:0.625rem; }
    .dcol-announcer { width:5.5rem; flex-shrink:0; }
    .announcer-chip { font-size:0.625rem; font-weight:600; padding:0.125rem 0.5rem; border-radius:9999px; background:hsl(var(--muted)/0.5); color:hsl(var(--muted-foreground)); }
    .announcer-won { color:hsl(140 60% 45%); background:hsl(140 60% 45% / 0.12); }
    .announcer-lost { color:hsl(0 70% 55%); background:hsl(0 70% 50% / 0.12); }
    .dcol-dealer { flex:1; min-width:5rem; font-size:0.75rem; color:hsl(var(--muted-foreground)); overflow:hidden; white-space:nowrap; text-overflow:ellipsis; }
    .dcol-pts { width:4.5rem; flex-shrink:0; text-align:center; font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }
    .pts-lead { color:hsl(var(--foreground)); font-weight:700; }
    .dcol-notes { width:7rem; flex-shrink:0; display:flex; gap:0.25rem; justify-content:flex-end; }
    .dcol-empty { color:hsl(var(--muted-foreground)/0.5); }
    .flag-sweep { display:inline-flex; align-items:center; gap:0.1875rem; color:hsl(var(--gold)); background:hsl(var(--gold)/0.12); }
    .flag-instant { display:inline-flex; align-items:center; gap:0.1875rem; color:hsl(265 70% 65%); background:hsl(265 70% 60% / 0.12); }

    /* Pagination */
    .pagination { display:flex; align-items:center; justify-content:center; gap:1rem; margin-top:1rem; }
    .page-btn { font-size:0.75rem; font-weight:600; color:hsl(var(--foreground)); background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.5rem; padding:0.375rem 0.875rem; cursor:pointer; }
    .page-btn:disabled { opacity:0.4; cursor:default; }
    .page-info { font-size:0.75rem; color:hsl(var(--muted-foreground)); font-variant-numeric:tabular-nums; }

    @media (max-width:640px) {
      .col-date, .col-deals { display:none; }
      .row { min-width:38rem; }
    }
  `],
})
export class AdminGamesComponent implements OnInit {
  readonly DicesIcon = Dices;
  readonly XIcon = X;
  readonly TrophyIcon = Trophy;
  readonly CheckIcon = Check;
  readonly ZapIcon = Zap;

  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private static readonly PAGE_SIZE = 25;

  readonly games = signal<AdminGameEntry[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly filterUserId = signal<string | null>(null);
  readonly filterUserName = signal<string | null>(null);
  readonly selectedGame = signal<AdminGameEntry | null>(null);
  readonly deals = signal<AdminDealEntry[]>([]);
  readonly dealsLoading = signal(false);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / AdminGamesComponent.PAGE_SIZE)));

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.filterUserId.set(params.get('userId'));
      this.filterUserName.set(params.get('name'));
      this.page.set(1);
      this.load();
    });
  }


  teamPlayers(game: AdminGameEntry, team: string): AdminGamePlayerEntry[] {
    return game.players.filter((p) => p.team === team);
  }

  filterByPlayer(player: AdminGamePlayerEntry, event: Event): void {
    if (player.isBot || !player.userId) return;
    event.stopPropagation();
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { userId: player.userId, name: player.displayName },
    });
  }

  openDetail(game: AdminGameEntry): void {
    this.selectedGame.set(game);
    this.deals.set([]);
    this.dealsLoading.set(true);
    this.api.getAdminGameDeals(game.id).subscribe({
      next: (res) => {
        this.deals.set(res.deals);
        this.dealsLoading.set(false);
      },
      error: () => this.dealsLoading.set(false),
    });
  }

  closeDetail(): void {
    this.selectedGame.set(null);
  }

  teamLabel(game: AdminGameEntry, team: string): string {
    return this.teamPlayers(game, team).map((p) => p.displayName).join(' & ');
  }

  playerAt(game: AdminGameEntry, position: PlayerPosition): string {
    return game.players.find((p) => p.position === position)?.displayName ?? '–';
  }

  announcerNames(game: AdminGameEntry, deal: AdminDealEntry): string {
    return deal.announcerTeam ? this.teamLabel(game, deal.announcerTeam) : '';
  }

  isColourMode(mode: string): boolean {
    return mode.startsWith('Colour');
  }

  clearFilter(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  setPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAdminGames(this.filterUserId(), this.page(), AdminGamesComponent.PAGE_SIZE).subscribe({
      next: (res) => {
        this.games.set(res.games);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
