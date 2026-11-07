import { AfterViewInit, Component, computed, ElementRef, inject, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { GameMode, PlayerPosition, Team } from '../../api/generated/signalr-types.generated';
import {
  ApiService,
  MatchSummaryDealResponse,
  MatchSummaryNegotiationAction,
  MatchSummaryResponse,
} from '../../core/services/api.service';
import { GameModeIconComponent } from '../../shared/components/game-mode-icon/game-mode-icon.component';
import { MultiplierBadgeComponent } from '../../shared/components/multiplier-badge/multiplier-badge.component';

interface ChartPoint { x: number; y1: number; y2: number; deal: number }

/**
 * Scoreboard of a finished match: header with both teams and the final score, a score race
 * chart, headline statistics, per-player totals and the deal-by-deal table with negotiation,
 * points and trick winners. Reached from the match history ("Scoreboard & stats").
 */
@Component({
  selector: 'app-match-summary',
  standalone: true,
  imports: [RouterLink, TranslocoDirective, SkeletonModule, TagModule, GameModeIconComponent, MultiplierBadgeComponent],
  template: `
    <ng-container *transloco="let t">
      @if (loading()) {
        <div class="stack">
          <p-skeleton height="9rem" borderRadius="24px" />
          <p-skeleton height="16rem" borderRadius="24px" />
          <p-skeleton height="20rem" borderRadius="24px" />
        </div>
      } @else if (error()) {
        <div class="g-card error-card">
          <span class="g-empty-icon"><i class="pi pi-exclamation-circle"></i></span>
          <span>{{ t('matchSummary.error') }}</span>
          <a routerLink="/history" class="back">{{ t('matchSummary.back') }}</a>
        </div>
      } @else if (summary(); as s) {
        <div class="stack">
          <!-- Header -->
          <section class="g-card hero">
            <a routerLink="/history" class="back"><i class="pi pi-arrow-left"></i> {{ t('matchSummary.back') }}</a>
            <div class="hero-grid">
              <div class="team team1" [class.winner]="s.winnerTeam === 'Team1'">
                <span class="team-dot"></span>
                @for (p of teamPlayers(s, 'Team1'); track p.position) {
                  <span class="team-name">
                    {{ p.displayName }}
                    @if (p.position === myPosition()) { <em>{{ t('matchSummary.you') }}</em> }
                  </span>
                }
              </div>
              <div class="score">
                <span class="score-num team1" [class.winner]="s.winnerTeam === 'Team1'">{{ s.team1FinalScore }}</span>
                <span class="score-sep">:</span>
                <span class="score-num team2" [class.winner]="s.winnerTeam === 'Team2'">{{ s.team2FinalScore }}</span>
              </div>
              <div class="team team2 right" [class.winner]="s.winnerTeam === 'Team2'">
                <span class="team-dot"></span>
                @for (p of teamPlayers(s, 'Team2'); track p.position) {
                  <span class="team-name">
                    {{ p.displayName }}
                    @if (p.position === myPosition()) { <em>{{ t('matchSummary.you') }}</em> }
                  </span>
                }
              </div>
            </div>
            <div class="hero-meta">
              <p-tag [value]="s.isRanked ? t('matchSummary.ranked') : t('matchSummary.casual')" [severity]="s.isRanked ? 'info' : 'secondary'" [rounded]="true" />
              @if (s.wasAbandoned) { <p-tag [value]="t('settings.history.abandoned')" severity="warn" [rounded]="true" /> }
              <span>{{ formatDate(s.playedAt) }}</span>
              <span>· {{ t('settings.history.deals', { count: s.totalDeals }) }}</span>
              @if (s.durationSeconds !== null) { <span>· {{ formatDuration(s.durationSeconds) }}</span> }
              <span>· {{ t('matchSummary.target', { target: s.targetScore }) }}</span>
            </div>
          </section>

          <!-- Score race -->
          <section class="g-card chart-card">
            <div class="card-head">
              <span class="card-title">{{ t('matchSummary.chart.title') }}</span>
              <div class="legend">
                <span class="legend-item"><span class="swatch team1"></span>{{ teamLabel(s, 'Team1') }}</span>
                <span class="legend-item"><span class="swatch team2"></span>{{ teamLabel(s, 'Team2') }}</span>
              </div>
            </div>
            <div class="chart-wrap" #chartWrap>
              <svg class="chart" [attr.viewBox]="'0 0 ' + W() + ' ' + H" [attr.height]="H"
                   (mousemove)="onChartMove($event)" (mouseleave)="hover.set(null)">
                @for (g of yTicks(); track g) {
                  <line class="grid" [attr.x1]="PAD.l" [attr.x2]="W() - PAD.r" [attr.y1]="yScale(g)" [attr.y2]="yScale(g)" />
                  <text class="axis" [attr.x]="PAD.l - 6" [attr.y]="yScale(g) + 3" text-anchor="end">{{ g }}</text>
                }
                <line class="target" [attr.x1]="PAD.l" [attr.x2]="W() - PAD.r" [attr.y1]="yScale(s.targetScore)" [attr.y2]="yScale(s.targetScore)" />
                <text class="axis target-label" [attr.x]="W() - PAD.r" [attr.y]="yScale(s.targetScore) - 4" text-anchor="end">{{ t('matchSummary.chart.target') }} {{ s.targetScore }}</text>
                @for (p of points(); track p.deal) {
                  @if (p.deal > 0 && (p.deal % xStep() === 0 || p.deal === s.deals.length)) {
                    <text class="axis" [attr.x]="p.x" [attr.y]="H - 6" text-anchor="middle">{{ p.deal }}</text>
                  }
                }
                <path class="line team1" [attr.d]="path(1)" />
                <path class="line team2" [attr.d]="path(2)" />
                @if (hover(); as h) {
                  <line class="crosshair" [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="PAD.t" [attr.y2]="H - PAD.b" />
                  <circle class="dot team1" [attr.cx]="h.x" [attr.cy]="yScale(h.y1)" r="4" />
                  <circle class="dot team2" [attr.cx]="h.x" [attr.cy]="yScale(h.y2)" r="4" />
                }
                @if (lastPoint(); as lp) {
                  <text class="end-label team1" [attr.x]="lp.x + 6" [attr.y]="endLabelY(lp, 1)">{{ lp.y1 }}</text>
                  <text class="end-label team2" [attr.x]="lp.x + 6" [attr.y]="endLabelY(lp, 2)">{{ lp.y2 }}</text>
                }
              </svg>
              @if (hover(); as h) {
                <div class="tooltip" [style.left.px]="h.x" [class.flip]="h.x > W() * 0.7">
                  <span class="tooltip-title">{{ h.deal === 0 ? t('matchSummary.chart.start') : t('matchSummary.chart.deal', { n: h.deal }) }}</span>
                  <span><span class="swatch team1"></span>{{ h.y1 }}</span>
                  <span><span class="swatch team2"></span>{{ h.y2 }}</span>
                </div>
              }
            </div>
          </section>

          <!-- Headline stats -->
          <section class="tiles">
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.leadChanges') }}</span>
              <span class="tile-value">{{ s.stats.leadChanges }}</span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.biggestSwing') }}</span>
              @if (s.stats.biggestSwingDeal !== null) {
                <span class="tile-value" [class]="'tile-value ' + teamClass(s.stats.biggestSwingTeam)">+{{ s.stats.biggestSwingPoints }}</span>
                <span class="tile-sub">{{ t('matchSummary.chart.deal', { n: s.stats.biggestSwingDeal }) }} · {{ teamLabel(s, s.stats.biggestSwingTeam) }}</span>
              } @else { <span class="tile-value">–</span> }
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.longestRun') }}</span>
              <span class="tile-value" [class]="'tile-value ' + teamClass(s.stats.longestRunTeam)">{{ s.stats.longestRunDeals }}</span>
              @if (s.stats.longestRunTeam) { <span class="tile-sub">{{ t('matchSummary.stats.longestRunSub', { team: teamLabel(s, s.stats.longestRunTeam) }) }}</span> }
            </div>
            @if (s.stats.comebackFrom > 0) {
              <div class="tile">
                <span class="tile-label">{{ t('matchSummary.stats.comeback') }}</span>
                <span class="tile-value" [class]="'tile-value ' + teamClass(s.winnerTeam)">−{{ s.stats.comebackFrom }}</span>
                <span class="tile-sub">{{ t('matchSummary.stats.comebackSub') }}</span>
              </div>
            }
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.contracts') }}</span>
              <span class="tile-value split">
                <span class="team1">{{ s.stats.team1ContractsMade }}<small>/{{ s.stats.team1Contracts }}</small></span>
                <span class="team2">{{ s.stats.team2ContractsMade }}<small>/{{ s.stats.team2Contracts }}</small></span>
              </span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.tricks') }}</span>
              <span class="tile-value split"><span class="team1">{{ s.stats.team1Tricks }}</span><span class="team2">{{ s.stats.team2Tricks }}</span></span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.sweeps') }}</span>
              <span class="tile-value split"><span class="team1">{{ s.stats.team1Sweeps }}</span><span class="team2">{{ s.stats.team2Sweeps }}</span></span>
            </div>
            <div class="tile">
              <span class="tile-label">{{ t('matchSummary.stats.doubled') }}</span>
              <span class="tile-value">{{ s.stats.doubledDeals }}</span>
              <span class="tile-sub modes">
                @for (m of modesPlayed(s); track m.mode) {
                  <span class="mode-chip"><app-game-mode-icon [mode]="m.mode" size="0.9rem" />{{ m.count }}</span>
                }
              </span>
            </div>
          </section>

          <!-- Players -->
          <section class="g-card players-card">
            <span class="card-title">{{ t('matchSummary.players.title') }}</span>
            <div class="players">
              @for (p of s.players; track p.position) {
                <div class="player-row">
                  <span class="team-dot" [class]="'team-dot ' + teamClass(p.team)"></span>
                  <span class="player-name">{{ p.displayName }} @if (p.position === myPosition()) { <em>{{ t('matchSummary.you') }}</em> }</span>
                  <div class="bar"><div class="bar-fill" [class]="'bar-fill ' + teamClass(p.team)" [style.width.%]="maxTricks(s) ? (p.tricksWon / maxTricks(s)) * 100 : 0"></div></div>
                  <span class="player-stat">{{ p.tricksWon }} <small>{{ t('matchSummary.players.tricks') }}</small></span>
                  <span class="player-stat">{{ p.contractsMade }}/{{ p.contracts }} <small>{{ t('matchSummary.players.contracts') }}</small></span>
                </div>
              }
            </div>
          </section>

          <!-- Deals -->
          <section class="g-card deals-card">
            <span class="card-title">{{ t('matchSummary.deals.title') }}</span>
            <div class="deals-head">
              <span>#</span>
              <span>{{ t('matchSummary.deals.contract') }}</span>
              <span class="num">{{ t('matchSummary.deals.cardPoints') }}</span>
              <span class="num">{{ t('matchSummary.deals.matchPoints') }}</span>
              <span class="num">{{ t('matchSummary.deals.score') }}</span>
              <span></span>
            </div>
            @for (d of s.deals; track d.dealNumber) {
              <article class="deal" [class.open]="expanded() === d.dealNumber">
                <button type="button" class="deal-row" (click)="toggle(d.dealNumber)">
                  <span class="deal-num">{{ d.dealNumber }}</span>
                  <span class="contract">
                    @if (d.gameMode) {
                      <app-game-mode-icon [mode]="d.gameMode" size="1.25rem" />
                      <span class="contract-text">
                        <span class="contract-mode">{{ t(modeKey(d.gameMode)) }}
                          @if (d.multiplier !== 'Normal') { <app-multiplier-badge [multiplier]="d.multiplier" /> }
                        </span>
                        @if (d.announcer !== null) {
                          <span class="contract-by" [class]="'contract-by ' + teamClass(d.announcerTeam)">{{ t('matchSummary.deals.announcedBy', { name: nameOf(s, d.announcer) }) }}</span>
                        }
                      </span>
                      @if (d.announcerWon === false) { <i class="pi pi-times-circle flag fail" [title]="t('matchSummary.deals.failed')"></i> }
                      @if (d.wasSweep) { <i class="pi pi-bolt flag sweep" [title]="t('matchSummary.deals.sweep')"></i> }
                      @if (d.isInstantWin) { <i class="pi pi-star-fill flag instant" [title]="t('matchSummary.deals.instantWin')"></i> }
                    } @else {
                      <span class="contract-none">{{ t('matchSummary.deals.notPlayed') }}</span>
                    }
                  </span>
                  <span class="num pts">
                    @if (d.team1CardPoints !== null) {
                      <span class="team1">{{ d.team1CardPoints }}</span><span class="sep">:</span><span class="team2">{{ d.team2CardPoints }}</span>
                    } @else { – }
                  </span>
                  <span class="num mp">
                    <span class="mp-val team1" [class.zero]="d.team1MatchPoints === 0">+{{ d.team1MatchPoints }}</span>
                    <span class="mp-val team2" [class.zero]="d.team2MatchPoints === 0">+{{ d.team2MatchPoints }}</span>
                  </span>
                  <span class="num running"><span class="team1">{{ d.team1ScoreAfter }}</span><span class="sep">:</span><span class="team2">{{ d.team2ScoreAfter }}</span></span>
                  <i class="pi pi-chevron-down chevron"></i>
                </button>

                @if (expanded() === d.dealNumber) {
                  <div class="deal-details">
                    <div class="detail-block">
                      <span class="detail-label">{{ t('matchSummary.deals.negotiation') }} · {{ t('matchSummary.deals.dealer', { name: nameOf(s, d.dealer) }) }}</span>
                      @if (d.negotiation.length) {
                        <div class="nego">
                          @for (a of d.negotiation; track $index) {
                            <span class="nego-chip" [class]="'nego-chip ' + actionClass(a) + ' ' + teamClass(teamOf(s, a.player))">
                              <span class="nego-who">{{ nameOf(s, a.player) }}</span>
                              <span class="nego-what">
                                {{ actionLabel(a) }}
                                @if (a.actionType === 'Announce' && a.mode) { <app-game-mode-icon [mode]="a.mode" size="0.9rem" /> }
                              </span>
                            </span>
                          }
                        </div>
                      } @else { <span class="muted">{{ t('matchSummary.deals.noRecord') }}</span> }
                    </div>
                    <div class="detail-block">
                      <span class="detail-label">{{ t('matchSummary.deals.tricks') }} · <span class="team1">{{ d.team1Tricks }}</span> – <span class="team2">{{ d.team2Tricks }}</span></span>
                      @if (d.trickWinners.length) {
                        <div class="tricks">
                          @for (w of d.trickWinners; track $index) {
                            <span class="trick" [class]="'trick ' + teamClass(teamOf(s, w))" [title]="nameOf(s, w)">{{ initial(s, w) }}</span>
                          }
                        </div>
                      } @else { <span class="muted">{{ t('matchSummary.deals.noRecord') }}</span> }
                    </div>
                    @if (d.isReplayable) {
                      <a class="replay-link" [routerLink]="['/replay/match', s.matchId, d.dealNumber]" [queryParams]="replayQuery()">
                        <i class="pi pi-play"></i> {{ t('matchSummary.deals.replay') }}
                      </a>
                    }
                  </div>
                }
              </article>
            }
          </section>
        </div>
      }
    </ng-container>
  `,
  styles: [`
    :host { display:block; }
    .stack { display:flex; flex-direction:column; gap:1rem; }
    .g-card { padding:1.25rem 1.5rem; }
    .error-card { display:flex; flex-direction:column; align-items:center; gap:0.75rem; padding:3rem 1rem; color:var(--text-color-secondary); }
    .back { display:inline-flex; align-items:center; gap:0.375rem; font-size:0.85rem; color:var(--text-color-secondary); text-decoration:none; }
    .back:hover { color:var(--text-color); }
    .muted { color:var(--text-color-secondary); font-size:0.85rem; }
    .team1 { color:hsl(var(--team1)); }
    .team2 { color:hsl(var(--team2)); }

    /* Header */
    .hero { display:flex; flex-direction:column; gap:1rem; }
    .hero-grid { display:grid; grid-template-columns:1fr auto 1fr; align-items:center; gap:1rem; }
    .team { display:flex; flex-direction:column; gap:0.125rem; min-width:0; position:relative; padding-left:0.875rem; }
    .team.right { align-items:flex-end; text-align:right; padding-left:0; padding-right:0.875rem; }
    .team .team-dot { position:absolute; left:0; top:0.4rem; width:0.5rem; height:0.5rem; border-radius:50%; }
    .team.right .team-dot { left:auto; right:0; }
    .team.team1 .team-dot { background:hsl(var(--team1)); }
    .team.team2 .team-dot { background:hsl(var(--team2)); }
    .team-name { font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:100%; }
    .team.winner .team-name { color:var(--text-color); }
    .team:not(.winner) .team-name { color:var(--text-color-secondary); }
    .team-name em, .player-name em { font-style:normal; font-size:0.65rem; text-transform:uppercase; letter-spacing:0.05em; color:var(--p-primary-color); margin-left:0.25rem; }
    .score { display:flex; align-items:baseline; gap:0.5rem; font-variant-numeric:tabular-nums; }
    .score-num { font-size:2.75rem; font-weight:800; line-height:1; opacity:0.55; }
    .score-num.winner { opacity:1; }
    .score-sep { font-size:1.75rem; color:var(--text-color-secondary); }
    .hero-meta { display:flex; flex-wrap:wrap; align-items:center; gap:0.5rem; font-size:0.8rem; color:var(--text-color-secondary); }

    /* Chart */
    .card-head { display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:0.5rem; margin-bottom:0.75rem; }
    .card-title { font-weight:600; font-size:1rem; }
    .legend { display:flex; gap:1rem; font-size:0.8rem; color:var(--text-color-secondary); }
    .legend-item { display:inline-flex; align-items:center; gap:0.375rem; }
    .swatch { display:inline-block; width:0.75rem; height:0.75rem; border-radius:0.2rem; }
    .swatch.team1 { background:hsl(var(--team1)); }
    .swatch.team2 { background:hsl(var(--team2)); }
    .chart-wrap { position:relative; }
    .chart { width:100%; display:block; overflow:visible; }
    .chart .grid { stroke:var(--surface-border); stroke-width:1; vector-effect:non-scaling-stroke; }
    .chart .axis { fill:var(--text-color-secondary); font-size:11px; }
    .chart .target { stroke:var(--text-color-secondary); stroke-dasharray:4 4; stroke-width:1; opacity:0.7; vector-effect:non-scaling-stroke; }
    .chart .target-label { font-size:10px; opacity:0.8; }
    .chart .line { fill:none; stroke-width:2; stroke-linejoin:round; vector-effect:non-scaling-stroke; }
    .chart .line.team1 { stroke:hsl(var(--team1)); }
    .chart .line.team2 { stroke:hsl(var(--team2)); }
    .chart .crosshair { stroke:var(--text-color-secondary); stroke-width:1; opacity:0.6; vector-effect:non-scaling-stroke; }
    .chart .dot { stroke:var(--p-surface-900); stroke-width:2; vector-effect:non-scaling-stroke; }
    .chart .dot.team1 { fill:hsl(var(--team1)); }
    .chart .dot.team2 { fill:hsl(var(--team2)); }
    .chart .end-label { font-size:11px; font-weight:700; }
    .chart .end-label.team1 { fill:hsl(var(--team1)); }
    .chart .end-label.team2 { fill:hsl(var(--team2)); }
    .tooltip { position:absolute; top:0.25rem; transform:translateX(0.75rem); display:flex; flex-direction:column; gap:0.125rem; padding:0.4rem 0.6rem; border-radius:0.5rem; background:var(--p-surface-800); border:1px solid var(--surface-border); font-size:0.75rem; font-variant-numeric:tabular-nums; pointer-events:none; white-space:nowrap; }
    .tooltip.flip { transform:translateX(calc(-100% - 0.75rem)); }
    .tooltip-title { font-weight:600; color:var(--text-color-secondary); }
    .tooltip .swatch { width:0.5rem; height:0.5rem; margin-right:0.375rem; }

    /* Tiles */
    .tiles { display:grid; grid-template-columns:repeat(2, 1fr); gap:0.75rem; }
    @media (min-width:768px) { .tiles { grid-template-columns:repeat(4, 1fr); } }
    .tile { display:flex; flex-direction:column; gap:0.2rem; background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:var(--radius); padding:0.85rem 1rem; }
    .tile-label { font-size:0.7rem; text-transform:uppercase; letter-spacing:0.05em; color:hsl(var(--muted-foreground)); }
    .tile-value { font-size:1.5rem; font-weight:700; color:hsl(var(--foreground)); font-variant-numeric:tabular-nums; display:flex; align-items:baseline; gap:0.35rem; }
    .tile-value.split { gap:0.75rem; }
    .tile-value small { font-size:0.85rem; font-weight:500; opacity:0.7; }
    .tile-sub { font-size:0.72rem; color:hsl(var(--muted-foreground)); }
    .tile-sub.modes { display:flex; flex-wrap:wrap; gap:0.375rem; margin-top:0.25rem; }
    .mode-chip { display:inline-flex; align-items:center; gap:0.2rem; padding:0.1rem 0.4rem; border-radius:999px; background:var(--p-surface-800); color:var(--text-color); font-size:0.7rem; }

    /* Players */
    .players-card { display:flex; flex-direction:column; gap:0.75rem; }
    .players { display:flex; flex-direction:column; gap:0.5rem; }
    .player-row { display:grid; grid-template-columns:auto minmax(6rem, 1fr) 2fr auto auto; align-items:center; gap:0.75rem; font-size:0.9rem; }
    .team-dot { width:0.5rem; height:0.5rem; border-radius:50%; display:inline-block; }
    .team-dot.team1 { background:hsl(var(--team1)); }
    .team-dot.team2 { background:hsl(var(--team2)); }
    .player-name { font-weight:500; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .bar { height:0.5rem; border-radius:999px; background:var(--p-surface-800); overflow:hidden; }
    .bar-fill { height:100%; border-radius:999px; transition:width 0.4s ease; }
    .bar-fill.team1 { background:hsl(var(--team1)); }
    .bar-fill.team2 { background:hsl(var(--team2)); }
    .player-stat { font-variant-numeric:tabular-nums; font-weight:600; white-space:nowrap; min-width:4.5rem; text-align:right; }
    .player-stat small { font-weight:400; color:var(--text-color-secondary); font-size:0.7rem; }
    @media (max-width:639px) { .player-row { grid-template-columns:auto 1fr auto auto; } .bar { display:none; } }

    /* Deals */
    .deals-card { display:flex; flex-direction:column; gap:0.5rem; }
    .deals-head, .deal-row { display:grid; grid-template-columns:2rem minmax(0, 1fr) 5.5rem 5rem 5rem 1.25rem; align-items:center; gap:0.75rem; }
    .deals-head { padding:0.25rem 0.75rem; font-size:0.68rem; text-transform:uppercase; letter-spacing:0.05em; color:var(--text-color-secondary); }
    .num { text-align:right; font-variant-numeric:tabular-nums; }
    .deal { border:1px solid var(--surface-border); border-radius:0.875rem; background:var(--p-surface-900); overflow:hidden; }
    .deal.open { border-color:var(--p-surface-600); }
    .deal-row { width:100%; padding:0.625rem 0.75rem; border:none; background:transparent; color:inherit; text-align:left; cursor:pointer; }
    .deal-row:hover { background:var(--surface-hover); }
    .deal-num { font-weight:700; color:var(--text-color-secondary); }
    .contract { display:flex; align-items:center; gap:0.5rem; min-width:0; }
    .contract-text { display:flex; flex-direction:column; min-width:0; }
    .contract-mode { display:inline-flex; align-items:center; gap:0.375rem; font-weight:600; font-size:0.9rem; }
    .contract-by { font-size:0.72rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .contract-none { color:var(--text-color-secondary); font-size:0.85rem; }
    .flag { font-size:0.9rem; }
    .flag.fail { color:var(--p-red-400); }
    .flag.sweep { color:var(--p-primary-color); }
    .flag.instant { color:var(--p-amber-400); }
    .pts { font-size:0.85rem; color:var(--text-color-secondary); }
    .pts .team1, .pts .team2 { color:var(--text-color); }
    .sep { margin:0 0.2rem; opacity:0.5; }
    .mp { display:flex; justify-content:flex-end; gap:0.375rem; }
    .mp-val { font-weight:700; min-width:1.75rem; text-align:right; }
    .mp-val.zero { opacity:0.3; }
    .running { font-weight:600; }
    .chevron { color:var(--text-color-secondary); font-size:0.7rem; transition:transform var(--transition-duration); }
    .open .chevron { transform:rotate(180deg); }
    .deal-details { display:flex; flex-direction:column; gap:0.875rem; padding:0.25rem 0.75rem 1rem; }
    .detail-block { display:flex; flex-direction:column; gap:0.5rem; }
    .detail-label { font-size:0.7rem; text-transform:uppercase; letter-spacing:0.05em; color:var(--text-color-secondary); }
    .nego { display:flex; flex-wrap:wrap; gap:0.375rem; }
    .nego-chip { display:inline-flex; flex-direction:column; padding:0.3rem 0.6rem; border-radius:0.625rem; background:var(--p-surface-800); border-left:3px solid transparent; font-size:0.75rem; }
    .nego-chip.team1 { border-left-color:hsl(var(--team1)); }
    .nego-chip.team2 { border-left-color:hsl(var(--team2)); }
    .nego-who { color:var(--text-color-secondary); font-size:0.68rem; }
    .nego-what { display:inline-flex; align-items:center; gap:0.25rem; font-weight:600; color:var(--text-color); }
    .nego-chip.accept .nego-what { font-weight:400; color:var(--text-color-secondary); }
    .nego-chip.double .nego-what { color:var(--p-amber-400); }
    .tricks { display:flex; gap:0.375rem; flex-wrap:wrap; }
    .trick { display:inline-flex; align-items:center; justify-content:center; width:1.75rem; height:1.75rem; border-radius:50%; font-size:0.72rem; font-weight:700; color:#fff; }
    .trick.team1 { background:hsl(var(--team1)); }
    .trick.team2 { background:hsl(var(--team2)); }
    .replay-link { display:inline-flex; align-items:center; gap:0.375rem; font-size:0.85rem; color:var(--p-sky-300); text-decoration:none; }
    .replay-link:hover { text-decoration:underline; }
    @media (max-width:639px) {
      .deals-head { display:none; }
      .deal-row { grid-template-columns:1.25rem minmax(0, 1fr) 3.75rem 3.25rem 1rem; gap:0.5rem; }
      .pts { display:none; }
      .running { font-size:0.85rem; }
    }
  `],
})
export class MatchSummaryComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  /** Chart width in CSS px, tracked from the container so one SVG unit is one pixel. */
  readonly W = signal(600);
  readonly H = 220;
  private readonly chartWrap = viewChild<ElementRef<HTMLElement>>('chartWrap');
  private resize?: ResizeObserver;
  readonly PAD = { l: 36, r: 40, t: 16, b: 22 };

  readonly summary = signal<MatchSummaryResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly expanded = signal<number | null>(null);
  readonly hover = signal<ChartPoint | null>(null);

  /** The seat of the signed-in user in this match. */
  readonly myPosition = computed<PlayerPosition | null>(() => this.summary()?.viewerPosition ?? null);

  readonly points = computed<ChartPoint[]>(() => {
    const s = this.summary();
    if (!s) return [];
    const n = s.deals.length;
    const pts: ChartPoint[] = [{ x: this.PAD.l, y1: 0, y2: 0, deal: 0 }];
    s.deals.forEach((d, i) => pts.push({ x: this.xScale(i + 1, n), y1: d.team1ScoreAfter, y2: d.team2ScoreAfter, deal: d.dealNumber }));
    return pts;
  });

  readonly yMax = computed(() => {
    const s = this.summary();
    if (!s) return 150;
    const m = Math.max(s.targetScore, s.team1FinalScore, s.team2FinalScore);
    return Math.ceil(m / 25) * 25 + 10;
  });

  readonly yTicks = computed(() => {
    const max = this.yMax();
    const step = max > 200 ? 50 : 25;
    const ticks: number[] = [];
    for (let v = 0; v <= max; v += step) ticks.push(v);
    return ticks;
  });

  readonly xStep = computed(() => {
    const n = this.summary()?.deals.length ?? 0;
    return n > 24 ? 5 : n > 12 ? 2 : 1;
  });

  readonly lastPoint = computed(() => {
    const p = this.points();
    return p.length > 1 ? p[p.length - 1] : null;
  });

  ngAfterViewInit(): void {
    if (typeof ResizeObserver === 'undefined') return;
    this.resize = new ResizeObserver((entries) => {
      const w = Math.round(entries[0]?.contentRect.width ?? 0);
      if (w > 0) this.W.set(w);
    });
    this.observeChart();
  }

  ngOnDestroy(): void { this.resize?.disconnect(); }

  /** The chart element only exists once data is loaded, so re-attach after each render. */
  private observeChart(): void {
    const el = this.chartWrap()?.nativeElement;
    if (el) this.resize?.observe(el);
    else requestAnimationFrame(() => this.observeChart());
  }

  ngOnInit(): void {
    const matchId = this.route.snapshot.paramMap.get('matchId');
    if (!matchId) { this.error.set(true); this.loading.set(false); return; }
    this.api.getMatchSummary(matchId).subscribe({
      next: (s) => { this.summary.set(s); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); },
    });
  }

  // ── chart ──────────────────────────────────────────────────────────────
  xScale(deal: number, total: number): number {
    const span = this.W() - this.PAD.l - this.PAD.r;
    return this.PAD.l + (total === 0 ? 0 : (deal / total) * span);
  }

  yScale(v: number): number {
    const span = this.H - this.PAD.t - this.PAD.b;
    return this.H - this.PAD.b - (v / this.yMax()) * span;
  }

  /** Step-after path: a team's score only moves when a deal is scored. */
  path(team: 1 | 2): string {
    const pts = this.points();
    if (!pts.length) return '';
    let d = `M ${pts[0].x} ${this.yScale(0)}`;
    for (let i = 1; i < pts.length; i++) {
      const y = this.yScale(team === 1 ? pts[i].y1 : pts[i].y2);
      d += ` H ${pts[i].x} V ${y}`;
    }
    return d;
  }

  endLabelY(lp: ChartPoint, team: 1 | 2): number {
    const y1 = this.yScale(lp.y1);
    const y2 = this.yScale(lp.y2);
    const y = team === 1 ? y1 : y2;
    // Push labels apart when the two finals are close.
    if (Math.abs(y1 - y2) < 12) return team === 1 ? (y1 <= y2 ? y - 7 : y + 11) : (y2 < y1 ? y - 7 : y + 11);
    return y + 4;
  }

  onChartMove(ev: MouseEvent): void {
    const svg = ev.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    const x = ev.clientX - rect.left;
    const pts = this.points();
    if (!pts.length) return;
    let best = pts[0];
    for (const p of pts) if (Math.abs(p.x - x) < Math.abs(best.x - x)) best = p;
    this.hover.set(best);
  }

  // ── helpers ────────────────────────────────────────────────────────────
  toggle(n: number): void { this.expanded.set(this.expanded() === n ? null : n); }

  replayQuery(): Record<string, string> {
    const v = this.myPosition();
    return v ? { view: v } : {};
  }

  teamPlayers(s: MatchSummaryResponse, team: Team | 'Team1' | 'Team2') { return s.players.filter((p) => p.team === team); }

  teamLabel(s: MatchSummaryResponse, team: Team | 'Team1' | 'Team2' | null): string {
    if (!team) return '';
    return this.teamPlayers(s, team).map((p) => p.displayName).join(' & ');
  }

  teamClass(team: Team | 'Team1' | 'Team2' | null): string { return team === 'Team1' ? 'team1' : team === 'Team2' ? 'team2' : ''; }

  teamOf(s: MatchSummaryResponse, pos: PlayerPosition): Team | null { return s.players.find((p) => p.position === pos)?.team ?? null; }

  nameOf(s: MatchSummaryResponse, pos: PlayerPosition): string { return s.players.find((p) => p.position === pos)?.displayName ?? pos; }

  initial(s: MatchSummaryResponse, pos: PlayerPosition): string { return this.nameOf(s, pos).charAt(0).toUpperCase(); }

  maxTricks(s: MatchSummaryResponse): number { return Math.max(0, ...s.players.map((p) => p.tricksWon)); }

  modesPlayed(s: MatchSummaryResponse): { mode: GameMode; count: number }[] {
    return (Object.entries(s.stats.dealsByMode) as [GameMode, number][])
      .map(([mode, count]) => ({ mode, count }))
      .sort((a, b) => b.count - a.count);
  }

  modeKey(mode: GameMode): string {
    switch (mode) {
      case GameMode.ColourClubs: return 'game.modes.clubs';
      case GameMode.ColourDiamonds: return 'game.modes.diamonds';
      case GameMode.ColourHearts: return 'game.modes.hearts';
      case GameMode.ColourSpades: return 'game.modes.spades';
      case GameMode.NoTrumps: return 'game.modes.noTrumps';
      case GameMode.AllTrumps: return 'game.modes.allTrumps';
    }
  }

  actionClass(a: MatchSummaryNegotiationAction): string {
    return a.actionType === 'Accept' ? 'accept' : a.actionType === 'Announce' ? 'announce' : 'double';
  }

  actionLabel(a: MatchSummaryNegotiationAction): string {
    switch (a.actionType) {
      case 'Announce': return a.mode ? this.transloco.translate(this.modeKey(a.mode)) : this.transloco.translate('negotiation.announce');
      case 'Accept': return this.transloco.translate('negotiation.accept');
      case 'Double': return this.transloco.translate('negotiation.double');
      case 'Redouble': return this.transloco.translate('negotiation.redouble');
      case 'ReRedouble': return this.transloco.translate('negotiation.reRedouble');
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  formatDuration(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const sec = seconds % 60;
    return m > 0 ? `${m}m ${sec}s` : `${sec}s`;
  }
}
