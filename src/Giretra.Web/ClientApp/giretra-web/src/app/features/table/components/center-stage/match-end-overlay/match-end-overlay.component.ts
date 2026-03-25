import { Component, input, output, computed, inject, signal, effect } from '@angular/core';
import { Team, DealRecapResponse, AchievementEarnedDto, PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { EloChangeResponse } from '../../../../../core/services/api.service';
import { getTeamLabel } from '../../../../../core/utils';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, Trophy, ArrowUp, ArrowDown, Zap, Star } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TurnTimerComponent } from '../../../../../shared/components/turn-timer/turn-timer.component';
import { GameModeIconComponent } from '../../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { MultiplierBadgeComponent } from '../../../../../shared/components/multiplier-badge/multiplier-badge.component';

@Component({
  selector: 'app-match-end-overlay',
  standalone: true,
  imports: [HlmButton, LucideAngularModule, TranslocoDirective, TurnTimerComponent, GameModeIconComponent, MultiplierBadgeComponent],
  template: `
    <div class="overlay" *transloco="let t">
      <div class="modal">
        <div class="modal-body">
          <!-- Hero: trophy + result -->
          <div class="hero">
            @if (isWinner()) {
              <i-lucide [img]="TrophyIcon" [size]="40" [strokeWidth]="1.5" class="trophy-icon winner"></i-lucide>
              <h1 class="title">{{ t('matchEnd.youWin') }}</h1>
            } @else {
              <h1 class="title">{{ t('matchEnd.gameOver') }}</h1>
            }
            <p class="winner-text">{{ winnerLabel() }} {{ t('matchEnd.wins') }}</p>
          </div>

          <!-- Final score -->
          <div class="final-score">
            <div class="score-column">
              <span class="score-label">{{ team1Label() }}</span>
              <span class="score team1-score">{{ team1Points() }}</span>
            </div>
            <span class="divider">–</span>
            <div class="score-column">
              <span class="score-label">{{ team2Label() }}</span>
              <span class="score team2-score">{{ team2Points() }}</span>
            </div>
          </div>

          <!-- Deal recap -->
          @if (completedDeals()?.length) {
            <div class="deal-recap">
              <div class="recap-scroll">
                @for (deal of completedDeals()!; track $index) {
                  <div class="recap-row">
                    <span class="deal-num">{{ $index + 1 }}</span>
                    <span class="deal-mode">
                      <app-game-mode-icon [mode]="deal.gameMode" size="1rem" />
                      @if (deal.multiplier !== 'Normal') {
                        <app-multiplier-badge [multiplier]="deal.multiplier" />
                      }
                      @if (deal.wasSweep && isColourMode(deal.gameMode)) {
                        <span class="sweep-badge" [title]="t('matchEnd.sweep')">
                          <i-lucide [img]="ZapIcon" [size]="10" [strokeWidth]="2.5"></i-lucide>
                        </span>
                      }
                    </span>
                    <span class="deal-pts">
                      <span class="pts team1-pts" [class.winner]="deal.team1MatchPoints > deal.team2MatchPoints">{{ deal.team1MatchPoints }}</span>
                      <span class="pts-sep">–</span>
                      <span class="pts team2-pts" [class.winner]="deal.team2MatchPoints > deal.team1MatchPoints">{{ deal.team2MatchPoints }}</span>
                    </span>
                  </div>
                }
              </div>
            </div>
          } @else {
            <p class="deals-played">{{ t('matchEnd.dealsPlayed', { count: totalDeals() }) }}</p>
          }

          <!-- Achievements -->
          @if (visibleAchievements().length > 0) {
            <div class="achievements-section">
              <div class="achievements-header">
                <i-lucide [img]="StarIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                <span>{{ visibleAchievements().length === 1 ? t('matchEnd.achievementEarned') : t('matchEnd.achievementsEarned', { count: visibleAchievements().length }) }}</span>
              </div>
              <div class="achievements-list">
                @for (ach of visibleAchievements(); track ach.code) {
                  <div
                    class="achievement-pill"
                    [class.tier-high]="ach.tier >= 4"
                    [class.tier-mid]="ach.tier === 3"
                    [class.revealed]="revealedCodes().has(ach.code)"
                    [class.hidden-ach]="ach.isHidden && !revealedCodes().has(ach.code)"
                    [style.animation-delay]="$index * 200 + 'ms'"
                  >
                    <span class="ach-tier">
                      @for (_ of tierDots(ach.tier); track $index) {
                        <span class="tier-dot"></span>
                      }
                    </span>
                    <span class="ach-body">
                      @if (ach.isHidden && !revealedCodes().has(ach.code)) {
                        <span class="ach-name ach-hidden-text">???</span>
                      } @else {
                        <span class="ach-name">{{ ach.name }}</span>
                      }
                      @if (ach.dealNumber) {
                        <span class="ach-deal">{{ t('matchEnd.achievementDeal', { deal: ach.dealNumber }) }}</span>
                      }
                    </span>
                  </div>
                }
              </div>
            </div>
          }

        </div>

        <!-- Footer: elo + timer + actions (always visible, never scrolled) -->
        <div class="modal-footer">
          @if (showElo()) {
            <div class="elo-card" [class.elo-positive]="eloIsPositive()" [class.elo-negative]="eloIsNegative()">
              <div class="elo-change-row">
                <span class="elo-change-value">{{ eloChange()!.eloChange >= 0 ? '+' : '' }}{{ eloChange()!.eloChange }}</span>
                @if (eloIsPositive()) {
                  <i-lucide [img]="ArrowUpIcon" [size]="16" [strokeWidth]="2.5" class="elo-arrow-icon"></i-lucide>
                } @else if (eloIsNegative()) {
                  <i-lucide [img]="ArrowDownIcon" [size]="16" [strokeWidth]="2.5" class="elo-arrow-icon"></i-lucide>
                }
              </div>
              <div class="elo-rating-label">
                {{ t('matchEnd.rating') }}: {{ eloChange()!.eloAfter }}
              </div>
            </div>
          }
          @if (idleDeadline()) {
            <div class="idle-timer">
              <span class="idle-label">{{ t('matchEnd.autoClose') }}</span>
              <app-turn-timer [deadline]="idleDeadline()" (expired)="leaveTable.emit()" />
            </div>
          }

          <div class="actions">
            @if (!isWatcher()) {
              <button
                hlmBtn
                variant="default"
                [disabled]="waiting()"
                (click)="playAgain.emit()"
              >
                {{ waiting() ? t('matchEnd.waitingForOthers') : t('matchEnd.playAgain') }}
              </button>
            }
            <button
              hlmBtn
              variant="secondary"
              (click)="leaveTable.emit()"
            >
              {{ t('matchEnd.leaveTable') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 0.75rem;
      z-index: 100;
      animation: fadeIn 0.3s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .modal {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      width: min(360px, 100%);
      max-height: calc(100dvh - 1.5rem);
      display: flex;
      flex-direction: column;
      animation: scaleIn 0.3s ease;
      overflow: hidden;
    }

    @keyframes scaleIn {
      from { opacity: 0; transform: scale(0.9); }
      to { opacity: 1; transform: scale(1); }
    }

    .modal-body {
      flex: 1 1 auto;
      overflow-y: auto;
      padding: 1.5rem 1.5rem 0.75rem;
      text-align: center;
    }

    .modal-footer {
      flex: 0 0 auto;
      padding: 0.75rem 1.5rem 1.5rem;
      text-align: center;
    }

    /* ── Hero ── */

    .hero {
      margin-bottom: 0.75rem;
    }

    .trophy-icon {
      color: hsl(var(--muted-foreground));
      margin-bottom: 0.25rem;
    }

    .trophy-icon.winner {
      color: hsl(var(--gold));
    }

    .title {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0;
      color: hsl(var(--foreground));
      line-height: 1.2;
    }

    .winner-text {
      font-size: 0.875rem;
      color: hsl(var(--primary));
      margin: 0.25rem 0 0;
    }

    /* ── Final score ── */

    .final-score {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }

    .score-column {
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .score-label {
      font-size: 0.625rem;
      text-transform: uppercase;
      color: hsl(var(--muted-foreground));
      letter-spacing: 0.05em;
    }

    .score {
      font-size: 2.25rem;
      font-weight: 700;
      line-height: 1.1;
    }

    .team1-score { color: hsl(var(--team1)); }
    .team2-score { color: hsl(var(--team2)); }

    .divider {
      font-size: 1.5rem;
      color: hsl(var(--muted-foreground));
    }

    /* ── Deal recap ── */

    .deal-recap {
      margin-bottom: 0.75rem;
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      overflow: hidden;
    }

    .recap-scroll {
      max-height: 168px;
      overflow-y: auto;
    }

    .recap-row {
      display: flex;
      align-items: center;
      padding: 0.3rem 0.5rem;
      gap: 0.5rem;
    }

    .recap-row:not(:last-child) {
      border-bottom: 1px solid hsl(var(--border) / 0.3);
    }

    .deal-num {
      flex: 0 0 1.25rem;
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .deal-mode {
      flex: 1 1 auto;
      display: flex;
      align-items: center;
      gap: 0.25rem;
      min-width: 0;
    }

    .deal-pts {
      flex: 0 0 auto;
      display: flex;
      align-items: center;
      gap: 0.25rem;
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

    .deals-played {
      font-size: 0.8125rem;
      color: hsl(var(--muted-foreground));
      margin: 0 0 0.75rem;
    }

    /* ── Achievements ── */

    .achievements-section {
      margin-bottom: 0.75rem;
      animation: slideIn 0.3s ease;
    }

    @keyframes slideIn {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .achievements-header {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.375rem;
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--gold));
      margin-bottom: 0.5rem;
    }

    .achievements-list {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .achievement-pill {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem 0.625rem;
      border-radius: 0.5rem;
      background: hsl(var(--secondary));
      border: 1px solid hsl(var(--border));
      text-align: left;
      animation: pillIn 0.3s ease both;
    }

    @keyframes pillIn {
      from { opacity: 0; transform: scale(0.92); }
      to { opacity: 1; transform: scale(1); }
    }

    .achievement-pill.tier-mid {
      border-color: hsl(var(--gold) / 0.3);
    }

    .achievement-pill.tier-high {
      border-color: hsl(var(--gold) / 0.5);
      background: hsl(var(--gold) / 0.06);
      animation: pillIn 0.3s ease both, shimmer 3s ease-in-out infinite;
    }

    @keyframes shimmer {
      0%, 100% { box-shadow: 0 0 0 0 hsl(var(--gold) / 0); }
      50% { box-shadow: 0 0 8px 0 hsl(var(--gold) / 0.15); }
    }

    /* Hidden achievement pre-reveal */
    .achievement-pill.hidden-ach {
      border-style: dashed;
    }

    .achievement-pill.hidden-ach .ach-hidden-text {
      font-style: italic;
      color: hsl(var(--muted-foreground));
      letter-spacing: 0.1em;
    }

    /* Reveal transition */
    .achievement-pill .ach-name {
      transition: all 0.3s ease;
    }

    .ach-tier {
      display: flex;
      gap: 2px;
      flex-shrink: 0;
    }

    .tier-dot {
      width: 5px;
      height: 5px;
      border-radius: 9999px;
      background: hsl(var(--gold));
    }

    .ach-body {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .ach-name {
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      line-height: 1.3;
    }

    .ach-deal {
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground));
      line-height: 1.2;
    }

    /* ── ELO ── */

    .elo-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.125rem;
      padding: 0.5rem 1rem;
      border-radius: 0.5rem;
      border: 1px solid hsl(var(--border));
      margin-bottom: 0.75rem;
    }

    .elo-card.elo-positive {
      background: hsl(142 70% 45% / 0.1);
      border-color: hsl(142 70% 45% / 0.3);
    }

    .elo-card.elo-negative {
      background: hsl(0 72% 51% / 0.1);
      border-color: hsl(0 72% 51% / 0.3);
    }

    .elo-change-row {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .elo-change-value {
      font-size: 1.25rem;
      font-weight: 700;
    }

    .elo-positive .elo-change-value,
    .elo-positive .elo-arrow-icon { color: hsl(142 70% 45%); }
    .elo-negative .elo-change-value,
    .elo-negative .elo-arrow-icon { color: hsl(0 72% 51%); }

    .elo-rating-label {
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
    }

    /* ── Footer ── */

    .idle-timer {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }

    .idle-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .actions {
      display: flex;
      gap: 0.75rem;
      justify-content: center;
    }
  `],
})
export class MatchEndOverlayComponent {
  private readonly transloco = inject(TranslocoService);
  readonly TrophyIcon = Trophy;
  readonly ArrowUpIcon = ArrowUp;
  readonly ArrowDownIcon = ArrowDown;
  readonly ZapIcon = Zap;
  readonly StarIcon = Star;

  readonly winner = input<Team | null>(null);
  readonly myTeam = input<Team | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly team1Points = input<number>(0);
  readonly team2Points = input<number>(0);
  readonly totalDeals = input<number>(0);
  readonly completedDeals = input<DealRecapResponse[] | null>(null);
  readonly eloChange = input<EloChangeResponse | null>(null);
  readonly isRanked = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
  readonly idleDeadline = input<Date | null>(null);
  readonly waiting = input<boolean>(false);
  readonly achievements = input<AchievementEarnedDto[]>([]);

  private static readonly COLOUR_MODES = new Set(['ColourClubs', 'ColourDiamonds', 'ColourHearts', 'ColourSpades']);
  isColourMode(mode: string): boolean {
    return MatchEndOverlayComponent.COLOUR_MODES.has(mode);
  }

  readonly playAgain = output<void>();
  readonly leaveTable = output<void>();

  readonly showElo = computed(() => this.isRanked() && this.eloChange() !== null);
  readonly eloIsPositive = computed(() => (this.eloChange()?.eloChange ?? 0) > 0);
  readonly eloIsNegative = computed(() => (this.eloChange()?.eloChange ?? 0) < 0);

  readonly isWinner = computed(() => {
    const w = this.winner();
    const mt = this.myTeam();
    return w && mt && w === mt;
  });

  readonly winnerLabel = computed(() => {
    const w = this.winner();
    if (!w) return '';
    return getTeamLabel(w as 'Team1' | 'Team2', this.myTeam(), (k) => this.transloco.translate(k));
  });

  readonly team1Label = computed(() => getTeamLabel('Team1', this.myTeam(), (k) => this.transloco.translate(k)));
  readonly team2Label = computed(() => getTeamLabel('Team2', this.myTeam(), (k) => this.transloco.translate(k)));

  /** Filter achievements to current player only, sorted by tier descending */
  readonly myAchievements = computed(() => {
    const pos = this.myPosition();
    if (!pos) return [];
    return this.achievements()
      .filter(a => a.playerPosition === pos)
      .sort((a, b) => b.tier - a.tier);
  });

  /** Achievements currently visible (drives stagger — updated as reveals complete) */
  readonly visibleAchievements = this.myAchievements;

  /** Tracks which hidden achievements have been revealed */
  readonly revealedCodes = signal<Set<string>>(new Set());

  constructor() {
    // When achievements arrive, schedule reveal for hidden ones
    effect(() => {
      const achs = this.myAchievements();
      if (achs.length === 0) return;

      const hiddenCodes = achs.filter(a => a.isHidden).map(a => a.code);
      if (hiddenCodes.length === 0) return;

      // Reveal hidden achievements after a delay (stagger base + 400ms per achievement)
      hiddenCodes.forEach((code, i) => {
        const delay = (achs.length * 200) + 400 + (i * 300);
        setTimeout(() => {
          this.revealedCodes.update(set => {
            const next = new Set(set);
            next.add(code);
            return next;
          });
        }, delay);
      });
    });
  }

  tierDots(tier: number): number[] {
    return Array.from({ length: Math.min(tier, 5) });
  }
}
