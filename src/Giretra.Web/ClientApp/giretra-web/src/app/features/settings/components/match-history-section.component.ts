import { Component, inject, signal, OnInit } from '@angular/core';
import { LucideAngularModule, ChevronLeft, ChevronRight, Trophy } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { ApiService, MatchHistoryItemResponse } from '../../../core/services/api.service';

@Component({
  selector: 'app-match-history-section',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <div class="history" *transloco="let t">
      @if (matches().length === 0 && !loading()) {
        <div class="empty-state">{{ t('settings.history.noMatches') }}</div>
      }

      @for (match of matches(); track match.matchId) {
        <div class="match-card" (click)="toggleExpand(match.matchId)">
          <div class="match-header">
            <div class="match-main">
              <div class="match-date">{{ formatDate(match.playedAt) }}</div>
              <div class="match-room">{{ match.roomName }}</div>
            </div>
            <div class="match-score">
              <span class="score" [class.score-win]="match.isWinner && match.team === 'Team1'" [class.score-loss]="!match.isWinner && match.team === 'Team1'">{{ match.team1FinalScore }}</span>
              <span class="score-sep">-</span>
              <span class="score" [class.score-win]="match.isWinner && match.team === 'Team2'" [class.score-loss]="!match.isWinner && match.team === 'Team2'">{{ match.team2FinalScore }}</span>
            </div>
            <div class="match-badges">
              @if (match.isWinner) {
                <span class="badge badge-win">
                  <i-lucide [img]="TrophyIcon" [size]="10"></i-lucide>
                  {{ t('settings.history.win') }}
                </span>
              } @else {
                <span class="badge badge-loss">{{ t('settings.history.loss') }}</span>
              }
              @if (match.eloChange !== null) {
                <span class="badge" [class.badge-elo-up]="match.eloChange > 0" [class.badge-elo-down]="match.eloChange < 0">
                  {{ match.eloChange > 0 ? '+' : '' }}{{ match.eloChange }}
                </span>
              }
              @if (match.wasAbandoned) {
                <span class="badge badge-abandoned">{{ t('settings.history.abandoned') }}</span>
              }
            </div>
            <div class="match-meta">
              <span class="meta-item">{{ t('settings.history.deals', { count: match.totalDeals }) }}</span>
              @if (match.durationSeconds !== null) {
                <span class="meta-item">{{ formatDuration(match.durationSeconds) }}</span>
              }
            </div>
          </div>

          @if (expandedId() === match.matchId) {
            <div class="match-details">
              <div class="players-grid">
                @for (player of match.players; track player.position) {
                  <div class="player-detail" [class.player-winner]="player.isWinner">
                    <span class="player-pos">{{ player.position }}</span>
                    <span class="player-name">{{ player.displayName }}</span>
                    <span class="player-team" [class.team1]="player.team === 'Team1'" [class.team2]="player.team === 'Team2'">
                      {{ player.team === 'Team1' ? t('settings.history.team1') : t('settings.history.team2') }}
                    </span>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- Pagination -->
      @if (totalCount() > pageSize) {
        <div class="pagination">
          <button
            class="btn btn-sm"
            (click)="prevPage()"
            [disabled]="currentPage() <= 1"
          >
            <i-lucide [img]="ChevronLeftIcon" [size]="14"></i-lucide>
            {{ t('settings.history.prev') }}
          </button>
          <span class="page-info">{{ t('settings.history.pageInfo', { page: currentPage(), total: totalPages() }) }}</span>
          <button
            class="btn btn-sm"
            (click)="nextPage()"
            [disabled]="currentPage() >= totalPages()"
          >
            {{ t('settings.history.next') }}
            <i-lucide [img]="ChevronRightIcon" [size]="14"></i-lucide>
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .history { display:flex; flex-direction:column; gap:0.75rem; }
    .match-card { background:hsl(var(--secondary)); border:1px solid hsl(var(--border)); border-radius:var(--radius); overflow:hidden; cursor:pointer; transition:all 0.15s ease; }
    .match-card:hover { border-color:hsl(var(--foreground)/0.15); }
    .match-header { display:flex; align-items:center; gap:0.75rem; padding:0.75rem; flex-wrap:wrap; }
    .match-main { flex:1; min-width:0; }
    .match-date { font-size:0.625rem; color:hsl(var(--muted-foreground)); }
    .match-room { font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .match-score { display:flex; align-items:center; gap:0.25rem; font-size:1rem; font-weight:700; }
    .score-sep { color:hsl(var(--muted-foreground)); font-weight:400; }
    .score-win { color:hsl(var(--primary)); }
    .score-loss { color:hsl(var(--destructive)); }
    .match-badges { display:flex; gap:0.375rem; flex-shrink:0; }
    .badge { display:inline-flex; align-items:center; gap:0.1875rem; padding:0.125rem 0.5rem; border-radius:9999px; font-size:0.5625rem; font-weight:700; text-transform:uppercase; letter-spacing:0.04em; }
    .badge-win { background:hsl(var(--primary)/0.15); color:hsl(var(--primary)); }
    .badge-loss { background:hsl(var(--destructive)/0.15); color:hsl(var(--destructive)); }
    .badge-elo-up { background:hsl(var(--primary)/0.1); color:hsl(var(--primary)); }
    .badge-elo-down { background:hsl(var(--destructive)/0.1); color:hsl(var(--destructive)); }
    .badge-abandoned { background:hsl(var(--accent)/0.15); color:hsl(var(--accent)); }
    .match-meta { display:flex; gap:0.5rem; flex-shrink:0; }
    .meta-item { font-size:0.625rem; color:hsl(var(--muted-foreground)); }
    .match-details { padding:0 0.75rem 0.75rem; border-top:1px solid hsl(var(--border)/0.5); }
    .players-grid { display:grid; grid-template-columns:repeat(2, 1fr); gap:0.5rem; padding-top:0.75rem; }
    .player-detail { display:flex; align-items:center; gap:0.375rem; padding:0.375rem 0.5rem; border-radius:var(--radius); background:hsl(var(--card)); font-size:0.75rem; }
    .player-winner { border-left:2px solid hsl(var(--primary)); }
    .player-pos { font-size:0.625rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; min-width:3rem; }
    .player-name { flex:1; font-weight:500; color:hsl(var(--foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .player-team { font-size:0.5625rem; font-weight:600; padding:0.0625rem 0.375rem; border-radius:9999px; }
    .team1 { background:hsl(var(--team1)/0.15); color:hsl(var(--team1)); }
    .team2 { background:hsl(var(--team2)/0.15); color:hsl(var(--team2)); }
    .pagination { display:flex; align-items:center; justify-content:center; gap:1rem; padding-top:0.5rem; }
    .page-info { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .btn { display:inline-flex; align-items:center; gap:0.375rem; padding:0.375rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--secondary)); color:hsl(var(--foreground)); font-size:0.75rem; font-weight:500; cursor:pointer; transition:all 0.15s ease; }
    .btn:hover { background:hsl(var(--muted)); }
    .btn:disabled { opacity:0.4; cursor:not-allowed; }
    .btn-sm { padding:0.25rem 0.625rem; font-size:0.6875rem; }
    .empty-state { padding:2rem; text-align:center; color:hsl(var(--muted-foreground)); font-size:0.8125rem; }
    @media (max-width:480px) {
      .match-header { flex-direction:column; align-items:flex-start; }
      .players-grid { grid-template-columns:1fr; }
    }
  `],
})
export class MatchHistorySectionComponent implements OnInit {
  readonly ChevronLeftIcon = ChevronLeft;
  readonly ChevronRightIcon = ChevronRight;
  readonly TrophyIcon = Trophy;

  private readonly api = inject(ApiService);

  readonly matches = signal<MatchHistoryItemResponse[]>([]);
  readonly totalCount = signal(0);
  readonly currentPage = signal(1);
  readonly loading = signal(true);
  readonly expandedId = signal<string | null>(null);
  readonly pageSize = 20;

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }

  ngOnInit(): void {
    this.loadMatches();
  }

  private loadMatches(): void {
    this.loading.set(true);
    this.api.getMatchHistory(this.currentPage(), this.pageSize).subscribe({
      next: (res) => {
        this.matches.set(res.matches);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  toggleExpand(matchId: string): void {
    this.expandedId.set(this.expandedId() === matchId ? null : matchId);
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.set(this.currentPage() - 1);
      this.loadMatches();
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.set(this.currentPage() + 1);
      this.loadMatches();
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatDuration(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return m > 0 ? `${m}m ${s}s` : `${s}s`;
  }
}
