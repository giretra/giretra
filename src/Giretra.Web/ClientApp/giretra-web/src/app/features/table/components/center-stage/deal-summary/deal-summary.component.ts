import { Component, input, output, computed, inject, signal } from '@angular/core';
import { CardPointsBreakdownResponse, CardSuit, GameMode, PlayerPosition, Team } from '../../../../../api/generated/signalr-types.generated';
import { getTeamLabel, isRedSuit, toRelativePosition, getTeam } from '../../../../../core/utils';
import { cardToString } from '../../../../../core/models/card.model';
import { TrickHistoryEntry } from '../../../../../core/services/game-state.service';
import { GameModeBadgeComponent } from '../../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { GameModeIconComponent } from '../../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { HlmButton } from '@spartan-ng/helm/button';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

interface BreakdownRow {
  label: string;
  perCardValue: number;
  team1Points: number;
  team2Points: number;
  isTrump: boolean;
  trumpSuit?: CardSuit;
}

@Component({
  selector: 'app-deal-summary',
  standalone: true,
  imports: [GameModeBadgeComponent, GameModeIconComponent, HlmButton, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    @if (summary(); as s) {
      <div class="deal-summary">
        @if (!showingTrickHistory()) {
          <!-- ═══ Summary View ═══ -->
          <h2 class="title">{{ t('dealSummary.title') }}</h2>

          <div class="mode-display">
            <app-game-mode-badge [mode]="s.gameMode" size="1.5rem" />
          </div>

          <div class="scores-section">
            <div class="team-column team1">
              <span class="team-label">{{ team1Label() }}</span>
              <span class="card-points">{{ s.team1CardPoints }}</span>
              <span class="pts-label">{{ t('dealSummary.pts') }}</span>
              <span class="earned" [class.winner]="s.team1MatchPointsEarned > 0">
                +{{ s.team1MatchPointsEarned }} {{ t('dealSummary.match') }}
              </span>
            </div>

            <div class="divider"></div>

            <div class="team-column team2">
              <span class="team-label">{{ team2Label() }}</span>
              <span class="card-points">{{ s.team2CardPoints }}</span>
              <span class="pts-label">{{ t('dealSummary.pts') }}</span>
              <span class="earned" [class.winner]="s.team2MatchPointsEarned > 0">
                +{{ s.team2MatchPointsEarned }} {{ t('dealSummary.match') }}
              </span>
            </div>
          </div>

          <!-- Card Points Breakdown -->
          <div class="breakdown-section">
            <h3 class="breakdown-title">{{ t('dealSummary.pointsBreakdown') }}</h3>
            <table class="breakdown-table">
              <thead>
                <tr>
                  <th class="card-type">{{ t('dealSummary.card') }}</th>
                  <th class="value-col">{{ t('dealSummary.each') }}</th>
                  <th class="team1-col">{{ team1Label() }}</th>
                  <th class="team2-col">{{ team2Label() }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of breakdownRows(); track row.label + row.perCardValue) {
                  <tr [class.trump-row]="row.isTrump">
                    <td class="card-type">
                      @if (row.isTrump && row.trumpSuit) {
                        <span class="trump-label">
                          <app-game-mode-icon [suit]="row.trumpSuit" size="0.75rem" />
                          <span>{{ row.label }}</span>
                        </span>
                      } @else {
                        {{ row.label }}
                      }
                    </td>
                    <td class="value-col">{{ row.perCardValue }}</td>
                    <td class="team1-col" [class.has-points]="row.team1Points > 0">{{ row.team1Points }}</td>
                    <td class="team2-col" [class.has-points]="row.team2Points > 0">{{ row.team2Points }}</td>
                  </tr>
                }
                <tr class="last-trick-row">
                  <td class="card-type" colspan="2">{{ t('dealSummary.lastTrick') }}</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.lastTrickBonus > 0">{{ s.team1Breakdown.lastTrickBonus > 0 ? '+10' : '-' }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.lastTrickBonus > 0">{{ s.team2Breakdown.lastTrickBonus > 0 ? '+10' : '-' }}</td>
                </tr>
                <tr class="total-row">
                  <td class="card-type" colspan="2">{{ t('dealSummary.total') }}</td>
                  <td class="team1-col has-points">{{ s.team1Breakdown.total }}</td>
                  <td class="team2-col has-points">{{ s.team2Breakdown.total }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          @if (s.wasSweep) {
            <div class="sweep-banner">
              {{ t('dealSummary.sweepBy', { team: sweepLabel() }) }}
            </div>
          }

          <!-- Match progress -->
          <div class="match-progress">
            <div class="progress-team team1-text">
              <span class="progress-score">{{ s.team1TotalMatchPoints }}</span>
              <span class="progress-target">{{ t('dealSummary.target') }}</span>
            </div>
            <span class="progress-label">{{ t('dealSummary.matchScore') }}</span>
            <div class="progress-team team2-text">
              <span class="progress-score">{{ s.team2TotalMatchPoints }}</span>
              <span class="progress-target">{{ t('dealSummary.target') }}</span>
            </div>
          </div>

          <div class="button-row">
            @if (hasTrickHistory()) {
              <button hlmBtn variant="outline" class="history-btn" [disabled]="waiting()" (click)="showingTrickHistory.set(true)">
                {{ t('dealSummary.viewTrickHistory') }}
              </button>
            }
            <button hlmBtn variant="default" class="continue-button" [disabled]="waiting()" (click)="dismissed.emit()">
              {{ waiting() ? t('dealSummary.waitingForOthers') : t('common.continue') }}
            </button>
          </div>

        } @else {
          <!-- ═══ Trick History View ═══ -->
          <div class="history-top">
            <button class="back-btn" (click)="showingTrickHistory.set(false)">
              <span class="back-arrow">&#8592;</span>
            </button>
            <h2 class="title">{{ t('dealSummary.trickHistory') }}</h2>
            <div class="history-mode">
              <app-game-mode-badge [mode]="s.gameMode" size="1.25rem" />
            </div>
          </div>

          <div class="trick-list">
            @for (trick of trickRows(); track trick.trickNumber) {
              <div class="trick-row" [class.trick-mine]="trick.isMyTeam === true" [class.trick-theirs]="trick.isMyTeam === false">
                <div class="trick-head">
                  <span class="trick-num">#{{ trick.trickNumber }}</span>
                  <span class="trick-winner">{{ trick.winnerLabel }}</span>
                  <span class="trick-pts">{{ trick.pointsWon }}</span>
                </div>
                <div class="trick-cards">
                  @for (card of trick.cards; track $index) {
                    <div class="tc-card" [class.tc-winner]="card.isWinner">
                      <span class="tc-rank" [class.tc-red]="card.isRed">{{ card.cardText }}</span>
                      <span class="tc-player">{{ card.playerLabel }}</span>
                    </div>
                  }
                </div>
              </div>
            }
          </div>

          <button hlmBtn variant="default" class="continue-button" [disabled]="waiting()" (click)="dismissed.emit()">
            {{ waiting() ? t('dealSummary.waitingForOthers') : t('common.continue') }}
          </button>
        }
      </div>
    }
    </ng-container>
  `,
  styles: [`
    .deal-summary {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 1.5rem;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      min-width: 320px;
      max-width: 400px;
      animation: scaleIn 0.25s ease;
      position: relative;
      z-index: 10;
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.93);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }

    .title {
      font-size: 1.25rem;
      font-weight: 600;
      margin: 0 0 0.75rem 0;
      color: hsl(var(--foreground));
    }

    .mode-display {
      margin-bottom: 1rem;
    }

    .scores-section {
      display: flex;
      align-items: stretch;
      gap: 1.5rem;
      margin-bottom: 1rem;
      width: 100%;
    }

    .team-column {
      display: flex;
      flex-direction: column;
      align-items: center;
      flex: 1;
      padding: 0.5rem;
      border-radius: 0.375rem;
    }

    .team1 {
      background: hsl(var(--team1) / 0.1);
    }

    .team2 {
      background: hsl(var(--team2) / 0.1);
    }

    .team-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      margin-bottom: 0.125rem;
    }

    .card-points {
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      line-height: 1;
    }

    .pts-label {
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    .earned {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      margin-top: 0.25rem;
    }

    .earned.winner {
      color: hsl(var(--primary));
      font-weight: 600;
    }

    .divider {
      width: 1px;
      background: hsl(var(--border));
    }

    .breakdown-section {
      width: 100%;
      margin-bottom: 1rem;
    }

    .breakdown-title {
      font-size: 0.75rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      margin: 0 0 0.5rem 0;
      text-align: center;
    }

    .breakdown-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8rem;
    }

    .breakdown-table th,
    .breakdown-table td {
      padding: 0.25rem 0.5rem;
      text-align: center;
    }

    .breakdown-table th {
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      border-bottom: 1px solid hsl(var(--border));
    }

    .breakdown-table .card-type {
      text-align: left;
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .value-col {
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground) / 0.6);
      text-align: center;
      width: 2rem;
    }

    .breakdown-table th.value-col {
      font-size: 0.625rem;
    }

    .breakdown-table .team1-col {
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .team2-col {
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .has-points {
      color: hsl(var(--foreground));
      font-weight: 500;
    }

    .breakdown-table tbody tr:nth-child(even) {
      background: hsl(var(--muted) / 0.2);
    }

    .breakdown-table .trump-row {
      background: hsl(var(--gold) / 0.06);
    }

    .breakdown-table .trump-row .card-type {
      color: hsl(var(--gold));
    }

    .breakdown-table .trump-row .value-col {
      color: hsl(var(--gold) / 0.7);
    }

    .trump-label {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }

    .breakdown-table .last-trick-row {
      border-top: 1px dashed hsl(var(--border));
    }

    .breakdown-table .total-row {
      border-top: 1px solid hsl(var(--border));
      font-weight: 600;
    }

    .breakdown-table .total-row .card-type {
      color: hsl(var(--foreground));
    }

    .sweep-banner {
      background: hsl(var(--gold));
      color: hsl(220, 20%, 10%);
      padding: 0.375rem 1rem;
      border-radius: 0.25rem;
      font-weight: 700;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }

    .match-progress {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .progress-team {
      display: flex;
      align-items: baseline;
      gap: 0.25rem;
    }

    .progress-score {
      font-size: 1.5rem;
      font-weight: 700;
    }

    .progress-target {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .team1-text .progress-score {
      color: hsl(var(--team1));
    }

    .team2-text .progress-score {
      color: hsl(var(--team2));
    }

    .progress-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    /* ═══ Button Row ═══ */
    .button-row {
      display: flex;
      gap: 0.5rem;
      width: 100%;
      margin-top: 0.5rem;
    }

    .history-btn {
      flex: 1;
    }

    .continue-button {
      flex: 1;
      margin-top: 0.5rem;
    }

    .button-row .continue-button {
      margin-top: 0;
    }

    /* ═══ Trick History View ═══ */
    .history-top {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: 100%;
      margin-bottom: 0.75rem;
    }

    .history-top .title {
      flex: 1;
      margin: 0;
      text-align: center;
      font-size: 1.1rem;
    }

    .back-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      border: none;
      background: hsl(var(--muted) / 0.3);
      border-radius: 0.375rem;
      cursor: pointer;
      color: hsl(var(--foreground));
      font-size: 1rem;
      transition: background 0.15s;
    }

    .back-btn:hover {
      background: hsl(var(--muted) / 0.5);
    }

    .history-mode {
      width: 2rem;
      display: flex;
      justify-content: center;
    }

    .trick-list {
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      max-height: min(50vh, 420px);
      overflow-y: auto;
      margin-bottom: 0.5rem;
      padding-right: 0.25rem;
    }

    .trick-list::-webkit-scrollbar {
      width: 4px;
    }

    .trick-list::-webkit-scrollbar-thumb {
      background: hsl(var(--muted-foreground) / 0.3);
      border-radius: 2px;
    }

    .trick-row {
      padding: 0.375rem 0.5rem;
      border-radius: 0.375rem;
      background: hsl(var(--muted) / 0.15);
      border-left: 3px solid transparent;
    }

    .trick-row.trick-mine {
      background: hsl(var(--team1) / 0.08);
      border-left-color: hsl(var(--team1));
    }

    .trick-row.trick-theirs {
      background: hsl(var(--team2) / 0.08);
      border-left-color: hsl(var(--team2));
    }

    .trick-head {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      margin-bottom: 0.25rem;
      font-size: 0.75rem;
    }

    .trick-num {
      font-weight: 700;
      color: hsl(var(--foreground));
      font-size: 0.8rem;
    }

    .trick-winner {
      flex: 1;
      color: hsl(var(--muted-foreground));
    }

    .trick-pts {
      font-weight: 600;
      color: hsl(var(--foreground));
      font-size: 0.8rem;
      font-variant-numeric: tabular-nums;
    }

    .trick-cards {
      display: flex;
      justify-content: center;
      gap: 0.375rem;
    }

    .tc-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 3.5rem;
      padding: 0.25rem 0.375rem;
      border-radius: 0.25rem;
      border: 1px solid hsl(var(--border) / 0.5);
      background: hsl(var(--card));
    }

    .tc-card.tc-winner {
      border-color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.08);
    }

    .tc-rank {
      font-size: 0.875rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      line-height: 1.2;
    }

    .tc-rank.tc-red {
      color: #ef4444;
    }

    .tc-player {
      font-size: 0.5625rem;
      color: hsl(var(--muted-foreground));
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 100%;
      line-height: 1.2;
    }

    .tc-card.tc-winner .tc-player {
      color: hsl(var(--gold));
      font-weight: 500;
    }
  `],
})
export class DealSummaryComponent {
  private readonly transloco = inject(TranslocoService);
  readonly myTeam = input<Team | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);

  readonly summary = input<{
    gameMode: GameMode;
    team1CardPoints: number;
    team2CardPoints: number;
    team1MatchPointsEarned: number;
    team2MatchPointsEarned: number;
    team1TotalMatchPoints: number;
    team2TotalMatchPoints: number;
    wasSweep: boolean;
    sweepingTeam: Team | null;
    team1Breakdown: CardPointsBreakdownResponse;
    team2Breakdown: CardPointsBreakdownResponse;
    trickHistory: TrickHistoryEntry[];
  } | null>(null);

  readonly waiting = input<boolean>(false);

  readonly dismissed = output<void>();

  readonly showingTrickHistory = signal(false);

  readonly team1Label = computed(() => getTeamLabel('Team1', this.myTeam(), (k) => this.transloco.translate(k)));
  readonly team2Label = computed(() => getTeamLabel('Team2', this.myTeam(), (k) => this.transloco.translate(k)));
  readonly sweepLabel = computed(() => {
    const s = this.summary();
    if (!s?.sweepingTeam) return '';
    return getTeamLabel(s.sweepingTeam as 'Team1' | 'Team2', this.myTeam(), (k) => this.transloco.translate(k));
  });

  readonly hasTrickHistory = computed(() => {
    const s = this.summary();
    return !!s?.trickHistory?.length;
  });

  readonly trickRows = computed(() => {
    const s = this.summary();
    const myPos = this.myPosition() ?? PlayerPosition.Bottom;
    if (!s?.trickHistory?.length) return [];

    return s.trickHistory.map((entry, i) => {
      const prevT1 = i > 0 ? s.trickHistory[i - 1].team1CumulativePoints : 0;
      const prevT2 = i > 0 ? s.trickHistory[i - 1].team2CumulativePoints : 0;
      const pointsWon = (entry.team1CumulativePoints - prevT1) + (entry.team2CumulativePoints - prevT2);

      const winnerRel = toRelativePosition(entry.winner, myPos);

      const cards = entry.playedCards.map(pc => {
        const relPos = toRelativePosition(pc.player, myPos);
        return {
          cardText: cardToString(pc.card),
          playerLabel: this.transloco.translate(`positions.${relPos}`),
          isRed: isRedSuit(pc.card.suit),
          isWinner: pc.player === entry.winner,
        };
      });

      const winnerTeam = getTeam(entry.winner);
      const myTeam = this.myTeam();

      return {
        trickNumber: entry.trickNumber,
        winnerLabel: this.transloco.translate(`positions.${winnerRel}`),
        pointsWon,
        cards,
        isMyTeam: myTeam ? winnerTeam === myTeam : null,
      };
    });
  });

  private static readonly modeToSuit: Record<string, CardSuit> = {
    [GameMode.ColourClubs]: CardSuit.Clubs,
    [GameMode.ColourDiamonds]: CardSuit.Diamonds,
    [GameMode.ColourHearts]: CardSuit.Hearts,
    [GameMode.ColourSpades]: CardSuit.Spades,
  };

  private isColourMode(mode: GameMode): boolean {
    return mode in DealSummaryComponent.modeToSuit;
  }

  readonly breakdownRows = computed((): BreakdownRow[] => {
    const s = this.summary();
    if (!s) return [];

    const mode = s.gameMode;
    const t1 = s.team1Breakdown;
    const t2 = s.team2Breakdown;
    const rows: BreakdownRow[] = [];

    if (this.isColourMode(mode)) {
      const trumpSuit = DealSummaryComponent.modeToSuit[mode];

      // Split jacks: trump J = 20pts, non-trump J = 2pts each
      const t1TrumpJ = t1.jacks >= 20 ? 20 : 0;
      const t2TrumpJ = t2.jacks >= 20 ? 20 : 0;
      const t1OtherJ = t1.jacks - t1TrumpJ;
      const t2OtherJ = t2.jacks - t2TrumpJ;

      // Split nines: trump 9 = 14pts, non-trump 9 = 0pts
      const t1Trump9 = t1.nines > 0 ? 14 : 0;
      const t2Trump9 = t2.nines > 0 ? 14 : 0;

      // Ordered by strongest: Trump J(20), Trump 9(14), A(11), 10(10), K(4), Q(3), other J(2)
      if (t1TrumpJ > 0 || t2TrumpJ > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.jack'), perCardValue: 20, team1Points: t1TrumpJ, team2Points: t2TrumpJ, isTrump: true, trumpSuit });
      if (t1Trump9 > 0 || t2Trump9 > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.nine'), perCardValue: 14, team1Points: t1Trump9, team2Points: t2Trump9, isTrump: true, trumpSuit });
      if (t1.aces > 0 || t2.aces > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.aces'), perCardValue: 11, team1Points: t1.aces, team2Points: t2.aces, isTrump: false });
      if (t1.tens > 0 || t2.tens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.tens'), perCardValue: 10, team1Points: t1.tens, team2Points: t2.tens, isTrump: false });
      if (t1.kings > 0 || t2.kings > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.kings'), perCardValue: 4, team1Points: t1.kings, team2Points: t2.kings, isTrump: false });
      if (t1.queens > 0 || t2.queens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.queens'), perCardValue: 3, team1Points: t1.queens, team2Points: t2.queens, isTrump: false });
      if (t1OtherJ > 0 || t2OtherJ > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.jacks'), perCardValue: 2, team1Points: t1OtherJ, team2Points: t2OtherJ, isTrump: false });
      // Non-trump nines = 0pts, skip

    } else if (mode === GameMode.AllTrumps) {
      // All trump ranking: J(20) > 9(14) > A(11) > 10(10) > K(4) > Q(3)
      if (t1.jacks > 0 || t2.jacks > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.jacks'), perCardValue: 20, team1Points: t1.jacks, team2Points: t2.jacks, isTrump: false });
      if (t1.nines > 0 || t2.nines > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.nines'), perCardValue: 14, team1Points: t1.nines, team2Points: t2.nines, isTrump: false });
      if (t1.aces > 0 || t2.aces > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.aces'), perCardValue: 11, team1Points: t1.aces, team2Points: t2.aces, isTrump: false });
      if (t1.tens > 0 || t2.tens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.tens'), perCardValue: 10, team1Points: t1.tens, team2Points: t2.tens, isTrump: false });
      if (t1.kings > 0 || t2.kings > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.kings'), perCardValue: 4, team1Points: t1.kings, team2Points: t2.kings, isTrump: false });
      if (t1.queens > 0 || t2.queens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.queens'), perCardValue: 3, team1Points: t1.queens, team2Points: t2.queens, isTrump: false });

    } else {
      // NoTrumps: A(11) > 10(10) > K(4) > Q(3) > J(2) > 9(0, skip)
      if (t1.aces > 0 || t2.aces > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.aces'), perCardValue: 11, team1Points: t1.aces, team2Points: t2.aces, isTrump: false });
      if (t1.tens > 0 || t2.tens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.tens'), perCardValue: 10, team1Points: t1.tens, team2Points: t2.tens, isTrump: false });
      if (t1.kings > 0 || t2.kings > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.kings'), perCardValue: 4, team1Points: t1.kings, team2Points: t2.kings, isTrump: false });
      if (t1.queens > 0 || t2.queens > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.queens'), perCardValue: 3, team1Points: t1.queens, team2Points: t2.queens, isTrump: false });
      if (t1.jacks > 0 || t2.jacks > 0)
        rows.push({ label: this.transloco.translate('dealSummary.cards.jacks'), perCardValue: 2, team1Points: t1.jacks, team2Points: t2.jacks, isTrump: false });
    }

    return rows;
  });
}
