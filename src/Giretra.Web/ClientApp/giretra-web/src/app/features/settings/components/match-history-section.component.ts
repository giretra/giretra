import { RouterLink } from '@angular/router';
import { Component, inject, signal, OnInit } from '@angular/core';
import { TranslocoDirective, TranslocoPipe } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { ApiService, MatchHistoryItemResponse } from '../../../core/services/api.service';
import { getPositionTranslationKey } from '../../../core/utils/position-utils';

@Component({
  selector: 'app-match-history-section',
  standalone: true,
  imports: [RouterLink, TranslocoDirective, TranslocoPipe, TagModule, SkeletonModule, PaginatorModule],
  template: `
    <ng-container *transloco="let t">
      <div class="g-panel-head">
        <div class="g-panel-text">
          <span class="g-panel-title">{{ t('settings.tabs.history') }}</span>
          <span class="g-form-desc">{{ t('settings.history.hint') }}</span>
        </div>
      </div>
      <div class="g-divider mx"></div>
      <div class="g-panel-body">
      @if (loading()) {
        @for (i of [0, 1, 2]; track i) {
          <p-skeleton height="4.5rem" borderRadius="16px" />
        }
      } @else if (matches().length === 0) {
        <div class="g-empty compact">
          <span class="g-empty-icon"><i class="pi pi-history"></i></span>
          <span class="g-empty-hint">{{ t('settings.history.noMatches') }}</span>
        </div>
      }

      @for (match of matches(); track match.matchId) {
        <article class="match" [class.open]="expandedId() === match.matchId">
          <button type="button" class="match-row" (click)="toggleExpand(match.matchId)">
            <span class="result-disc" [class.win]="match.isWinner" [class.loss]="!match.isWinner">
              <i class="pi" [class.pi-trophy]="match.isWinner" [class.pi-times]="!match.isWinner"></i>
            </span>
            <div class="match-main">
              <span class="match-room" [title]="match.roomName">{{ t('settings.history.vsOpponents', { opponents: opponentNames(match) }) }}</span>
              <span class="match-meta">
                @if (partnerName(match); as partner) { {{ t('settings.history.withPartner', { partner }) }} · }
                {{ formatDate(match.playedAt) }} · {{ t('settings.history.deals', { count: match.totalDeals }) }}
                @if (match.durationSeconds !== null) { · {{ formatDuration(match.durationSeconds) }} }
              </span>
            </div>
            <div class="match-score">
              <span class="score" [class.mine]="match.team === 'Team1'" [class.win]="match.isWinner && match.team === 'Team1'">{{ match.team1FinalScore }}</span>
              <span class="score-sep">:</span>
              <span class="score" [class.mine]="match.team === 'Team2'" [class.win]="match.isWinner && match.team === 'Team2'">{{ match.team2FinalScore }}</span>
            </div>
            <div class="match-tags">
              @if (match.eloChange !== null) {
                <p-tag [value]="(match.eloChange > 0 ? '+' : '') + match.eloChange" [severity]="match.eloChange > 0 ? 'success' : match.eloChange < 0 ? 'danger' : 'secondary'" [rounded]="true" />
              }
              @if (match.wasAbandoned) {
                <p-tag [value]="t('settings.history.abandoned')" severity="warn" [rounded]="true" />
              }
            </div>
            <i class="pi pi-chevron-down chevron"></i>
          </button>

          @if (expandedId() === match.matchId) {
            <div class="match-details">
              <div class="ctas">
                @if (!match.wasAbandoned) {
                  <a class="replay-cta" [routerLink]="['/replay/match', match.matchId, 1]" [queryParams]="{ view: match.position }">
                    <span class="replay-cta-icon"><i class="pi pi-play"></i></span>
                    <span class="replay-cta-text">
                      <span class="replay-cta-title">{{ t('settings.history.replayCta') }}</span>
                      <span class="replay-cta-hint">{{ t('settings.history.replayCtaHint') }}</span>
                    </span>
                    <i class="pi pi-arrow-right replay-cta-arrow"></i>
                  </a>
                }
                <a class="replay-cta summary" [routerLink]="['/history/match', match.matchId]">
                  <span class="replay-cta-icon"><i class="pi pi-chart-bar"></i></span>
                  <span class="replay-cta-text">
                    <span class="replay-cta-title">{{ t('settings.history.summaryCta') }}</span>
                    <span class="replay-cta-hint">{{ t('settings.history.summaryCtaHint') }}</span>
                  </span>
                  <i class="pi pi-arrow-right replay-cta-arrow"></i>
                </a>
              </div>
              @for (player of match.players; track player.position) {
                <div class="player" [class.winner]="player.isWinner">
                  <span class="player-pos">{{ positionKey(player.position) | transloco }}</span>
                  <span class="player-name">{{ player.displayName }}</span>
                  <p-tag [value]="player.team === 'Team1' ? t('settings.history.team1') : t('settings.history.team2')" [severity]="player.team === 'Team1' ? 'info' : 'secondary'" [rounded]="true" />
                </div>
              }
            </div>
          }
        </article>
      }

      @if (totalCount() > pageSize) {
        <p-paginator
          [rows]="pageSize"
          [totalRecords]="totalCount()"
          [first]="(currentPage() - 1) * pageSize"
          [showFirstLastIcon]="false"
          (onPageChange)="onPage($event)"
        />
      }
      </div>
    </ng-container>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; }
    .g-panel-text { display:flex; flex-direction:column; gap:0.125rem; }
    .g-divider.mx { margin:0 1.5rem; }
    @media (min-width:1200px) { .g-divider.mx { margin:0 2rem; } }
    .g-panel-body { gap:0.5rem; }
    .g-empty.compact { padding:1.5rem 0.5rem; }
    .match { border:1px solid var(--surface-border); border-radius:1rem; background:var(--p-surface-900); overflow:hidden; transition:border-color var(--transition-duration); }
    .match.open { border-color:var(--p-surface-600); }
    .ctas { grid-column:1 / -1; display:grid; grid-template-columns:1fr; gap:0.5rem; }
    @media (min-width:640px) { .ctas { grid-template-columns:repeat(auto-fit, minmax(16rem, 1fr)); } }
    .replay-cta { display:flex; align-items:center; gap:0.875rem; padding:0.75rem 1rem; border-radius:0.875rem; text-decoration:none; color:var(--text-color);
      background:linear-gradient(120deg, color-mix(in srgb, var(--p-sky-500) 22%, var(--p-surface-800)), color-mix(in srgb, var(--p-violet-500) 14%, var(--p-surface-800)));
      border:1px solid color-mix(in srgb, var(--p-sky-400) 35%, transparent);
      transition:transform var(--transition-duration), border-color var(--transition-duration), box-shadow var(--transition-duration); }
    .replay-cta:hover { transform:translateY(-1px); border-color:color-mix(in srgb, var(--p-sky-400) 70%, transparent); box-shadow:0 6px 18px color-mix(in srgb, var(--p-sky-500) 22%, transparent); }
    .replay-cta:hover .replay-cta-arrow { transform:translateX(3px); }
    .replay-cta-icon { display:inline-flex; align-items:center; justify-content:center; width:2.5rem; height:2.5rem; border-radius:50%; flex-shrink:0; background:var(--p-sky-500); color:#fff; box-shadow:0 2px 10px color-mix(in srgb, var(--p-sky-500) 45%, transparent); }
    .replay-cta-icon i { font-size:0.9rem; margin-left:2px; }
    .replay-cta-text { display:flex; flex-direction:column; gap:0.125rem; flex:1; min-width:0; }
    .replay-cta-title { font-weight:600; font-size:0.95rem; }
    .replay-cta-hint { font-size:0.75rem; color:var(--text-color-secondary); }
    .replay-cta-arrow { color:var(--p-sky-300); transition:transform var(--transition-duration); flex-shrink:0; }
    .replay-cta.summary { background:linear-gradient(120deg, color-mix(in srgb, var(--p-emerald-500) 20%, var(--p-surface-800)), color-mix(in srgb, var(--p-teal-500) 12%, var(--p-surface-800))); border-color:color-mix(in srgb, var(--p-emerald-400) 35%, transparent); }
    .replay-cta.summary:hover { border-color:color-mix(in srgb, var(--p-emerald-400) 70%, transparent); box-shadow:0 6px 18px color-mix(in srgb, var(--p-emerald-500) 22%, transparent); }
    .replay-cta.summary .replay-cta-icon { background:var(--p-emerald-500); box-shadow:0 2px 10px color-mix(in srgb, var(--p-emerald-500) 45%, transparent); }
    .replay-cta.summary .replay-cta-icon i { margin-left:0; }
    .replay-cta.summary .replay-cta-arrow { color:var(--p-emerald-300); }
    @media (max-width:639px) { .replay-cta-hint { display:none; } }
    .match-row { display:flex; align-items:center; gap:0.875rem; width:100%; padding:0.75rem 1rem; border:none; background:transparent; color:inherit; text-align:left; cursor:pointer; transition:background-color var(--transition-duration); }
    .match-row:hover { background:var(--surface-hover); }
    .result-disc { display:inline-flex; align-items:center; justify-content:center; width:2.25rem; height:2.25rem; border-radius:0.75rem; flex-shrink:0; }
    .result-disc.win { background:color-mix(in srgb, var(--p-green-500) 18%, transparent); color:var(--p-green-400); }
    .result-disc.loss { background:color-mix(in srgb, var(--p-red-500) 14%, transparent); color:var(--p-red-400); }
    .match-main { display:flex; flex-direction:column; gap:0.125rem; flex:1; min-width:0; }
    .match-room { font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .match-meta { font-size:0.75rem; color:var(--text-color-secondary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .match-score { display:flex; align-items:baseline; gap:0.25rem; font-variant-numeric:tabular-nums; flex-shrink:0; }
    .score { font-size:1.125rem; font-weight:700; color:var(--text-color-secondary); }
    .score.mine { color:var(--text-color); }
    .score.win { color:var(--p-green-400); }
    .score-sep { color:var(--text-color-secondary); }
    .match-tags { display:flex; gap:0.375rem; flex-shrink:0; }
    .chevron { color:var(--text-color-secondary); font-size:0.75rem; transition:transform var(--transition-duration); flex-shrink:0; }
    .open .chevron { transform:rotate(180deg); }
    .match-details { display:grid; grid-template-columns:repeat(auto-fill, minmax(14rem, 1fr)); gap:0.5rem; padding:0.25rem 1rem 1rem; }
    .player { display:flex; align-items:center; gap:0.625rem; padding:0.5rem 0.75rem; border-radius:0.75rem; background:var(--p-surface-800); }
    .player.winner { box-shadow:inset 0 0 0 1px color-mix(in srgb, var(--p-green-500) 40%, transparent); }
    .player-pos { font-size:0.6875rem; text-transform:uppercase; letter-spacing:0.04em; color:var(--text-color-secondary); min-width:4.5rem; }
    .player-name { flex:1; min-width:0; font-weight:500; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    :host ::ng-deep .p-paginator { background:transparent; justify-content:flex-end; padding-top:0.5rem; }
    @media (max-width:639px) { .match-tags { display:none; } .match-row { gap:0.625rem; padding:0.625rem 0.75rem; } }
  `],
})
export class MatchHistorySectionComponent implements OnInit {

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

  onPage(event: PaginatorState): void {
    const page = (event.page ?? 0) + 1;
    if (page !== this.currentPage()) {
      this.currentPage.set(page);
      this.loadMatches();
    }
  }

  /** Opponents' names joined with " & ", used as the match title. */
  opponentNames(match: MatchHistoryItemResponse): string {
    return match.players
      .filter((p) => p.team !== match.team)
      .map((p) => p.displayName)
      .join(' & ');
  }

  /** The player's partner (same team, other seat), or null if unknown. */
  partnerName(match: MatchHistoryItemResponse): string | null {
    const partner = match.players.find((p) => p.team === match.team && p.position !== match.position);
    return partner?.displayName ?? null;
  }

  positionKey(position: string): string {
    return getPositionTranslationKey(position as any);
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
