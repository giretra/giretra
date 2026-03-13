import { Component, input, output, computed, inject } from '@angular/core';
import { GameMode, PlayerPosition, Team } from '../../../../api/generated/signalr-types.generated';
import { DealHistoryEntry, MultiplierState } from '../../../../core/services/game-state.service';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { LucideAngularModule, X, Zap } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { getPositionTranslationKey } from '../../../../core/utils/position-utils';
import { getTeamLabel } from '../../../../core/utils';

@Component({
  selector: 'app-match-history-popup',
  standalone: true,
  imports: [
    GameModeIconComponent,
    MultiplierBadgeComponent,
    LucideAngularModule,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop" (click)="closed.emit()"></div>
    <div class="popup-container" (click)="closed.emit()">
      <div class="popup-panel" (click)="$event.stopPropagation()">
        <!-- Close button -->
        <button class="close-btn" (click)="closed.emit()">
          <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>

        <!-- Header -->
        <div class="header">
          <h2 class="title">{{ t('matchHistory.title') }}</h2>
          <div class="current-score">
            <span class="score team1-score">{{ team1MatchPoints() }}</span>
            <span class="score-sep">–</span>
            <span class="score team2-score">{{ team2MatchPoints() }}</span>
          </div>
          <div class="team-labels">
            <span class="team-label team1-label">{{ team1Label() }}</span>
            <span class="team-label team2-label">{{ team2Label() }}</span>
          </div>
        </div>

        <!-- Deal history table -->
        @if (dealHistory().length > 0) {
          <div class="history-table">
            <div class="table-header">
              <span class="col-num">#</span>
              <span class="col-mode">{{ t('matchHistory.mode') }}</span>
              <span class="col-dealer">{{ t('matchHistory.dealer') }}</span>
              <span class="col-pts">{{ t('matchHistory.points') }}</span>
            </div>
            <div class="table-scroll">
              @for (deal of dealHistory(); track deal.dealNumber) {
                <div class="table-row">
                  <span class="col-num deal-num">{{ deal.dealNumber }}</span>
                  <span class="col-mode deal-mode">
                    <app-game-mode-icon [mode]="deal.gameMode" size="1rem" />
                    @if (deal.multiplier !== 'Normal') {
                      <app-multiplier-badge [multiplier]="deal.multiplier" />
                    }
                    @if (deal.wasSweep && isColourMode(deal.gameMode)) {
                      <span class="sweep-badge" [title]="t('matchHistory.sweep')">
                        <i-lucide [img]="ZapIcon" [size]="10" [strokeWidth]="2.5"></i-lucide>
                      </span>
                    }
                  </span>
                  <span class="col-dealer deal-dealer">{{ t(positionKey(deal.dealer)) }}</span>
                  <span class="col-pts deal-pts">
                    <span class="pts team1-pts" [class.winner]="deal.team1MatchPointsEarned > deal.team2MatchPointsEarned">{{ deal.team1MatchPointsEarned }}</span>
                    <span class="pts-sep">–</span>
                    <span class="pts team2-pts" [class.winner]="deal.team2MatchPointsEarned > deal.team1MatchPointsEarned">{{ deal.team2MatchPointsEarned }}</span>
                  </span>
                </div>
              }
            </div>
          </div>
        } @else {
          <p class="empty-state">{{ t('matchHistory.empty') }}</p>
        }
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    :host {
      display: contents;
    }

    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 100;
      background: rgba(0, 0, 0, 0.5);
      animation: fadeIn 0.2s ease;
    }

    .popup-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 110;
      pointer-events: none;
    }

    .popup-panel {
      pointer-events: auto;
      position: relative;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 1.5rem;
      max-width: 380px;
      width: calc(100% - 2rem);
      animation: scaleIn 0.25s ease;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .close-btn {
      position: absolute;
      top: 0.75rem;
      right: 0.75rem;
      background: none;
      border: none;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 0.25rem;
      transition: color 0.15s ease;
    }

    .close-btn:hover {
      color: hsl(var(--foreground));
    }

    /* Header */
    .header {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
    }

    .title {
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      margin: 0;
    }

    .current-score {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
    }

    .score {
      font-size: 1.75rem;
      font-weight: 700;
      line-height: 1;
    }

    .team1-score { color: hsl(var(--team1)); }
    .team2-score { color: hsl(var(--team2)); }

    .score-sep {
      font-size: 1.25rem;
      color: hsl(var(--muted-foreground));
    }

    .team-labels {
      display: flex;
      gap: 2rem;
    }

    .team-label {
      font-size: 0.625rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground));
    }

    .team1-label { color: hsl(var(--team1) / 0.7); }
    .team2-label { color: hsl(var(--team2) / 0.7); }

    /* Table */
    .history-table {
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      overflow: hidden;
    }

    .table-header {
      display: flex;
      align-items: center;
      padding: 0.375rem 0.5rem;
      background: hsl(var(--muted) / 0.3);
      border-bottom: 1px solid hsl(var(--border));
      font-size: 0.625rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground));
    }

    .table-scroll {
      max-height: 240px;
      overflow-y: auto;
    }

    .table-row {
      display: flex;
      align-items: center;
      padding: 0.3rem 0.5rem;
    }

    .table-row:not(:last-child) {
      border-bottom: 1px solid hsl(var(--border) / 0.3);
    }

    .col-num {
      flex: 0 0 1.5rem;
      text-align: center;
    }

    .col-mode {
      flex: 1 1 auto;
      display: flex;
      align-items: center;
      gap: 0.25rem;
      min-width: 0;
    }

    .col-dealer {
      flex: 0 0 4.5rem;
      text-align: center;
    }

    .col-pts {
      flex: 0 0 auto;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .deal-num {
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
    }

    .deal-dealer {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .deal-pts {
      font-size: 0.8125rem;
      font-variant-numeric: tabular-nums;
    }

    .pts {
      min-width: 1.25rem;
      text-align: center;
      color: hsl(var(--muted-foreground));
    }

    .pts.winner {
      font-weight: 700;
    }

    .team1-pts.winner { color: hsl(var(--team1)); }
    .team2-pts.winner { color: hsl(var(--team2)); }

    .pts-sep {
      color: hsl(var(--muted-foreground) / 0.5);
      font-size: 0.75rem;
    }

    .sweep-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 16px;
      height: 16px;
      border-radius: 9999px;
      background: hsl(var(--gold) / 0.2);
      color: hsl(var(--gold));
      flex-shrink: 0;
    }

    .empty-state {
      color: hsl(var(--muted-foreground));
      font-size: 0.875rem;
      text-align: center;
      margin: 0;
      font-style: italic;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.9);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }
  `],
})
export class MatchHistoryPopupComponent {
  readonly XIcon = X;
  readonly ZapIcon = Zap;
  private readonly transloco = inject(TranslocoService);

  readonly dealHistory = input<DealHistoryEntry[]>([]);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly myTeam = input<Team | null>(null);

  readonly closed = output<void>();

  private static readonly COLOUR_MODES = new Set([
    GameMode.ColourClubs,
    GameMode.ColourDiamonds,
    GameMode.ColourHearts,
    GameMode.ColourSpades,
  ]);

  isColourMode(mode: GameMode): boolean {
    return MatchHistoryPopupComponent.COLOUR_MODES.has(mode);
  }

  positionKey(position: PlayerPosition): string {
    return getPositionTranslationKey(position);
  }

  readonly team1Label = computed(() =>
    getTeamLabel('Team1', this.myTeam(), (k) => this.transloco.translate(k))
  );

  readonly team2Label = computed(() =>
    getTeamLabel('Team2', this.myTeam(), (k) => this.transloco.translate(k))
  );
}
