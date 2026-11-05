/**
 * Presentational 9:16 stage of a deal replay. Pure function of its inputs: given the replay
 * and a time `t`, it draws the table (seats, trick, hands, score, caption). Used by the
 * interactive player and, with `renderMode`, by the headless clip renderer.
 *
 * Design size is 540×960 CSS px; the host scales it to fit.
 */
import { Component, computed, input, ViewEncapsulation } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslocoDirective } from '@jsverse/transloco';
import { CardResponse, PlayerPosition, Team } from '../../api/generated/signalr-types.generated';
import { DealReplayResponse } from '../../core/services/api.service';
import { toRelativePosition, RelativePosition, getTeam } from '../../core/utils/position-utils';
import { TrickAreaComponent } from '../table/components/center-stage/trick-area/trick-area.component';
import { PlayerSeatComponent } from '../../shared/components/player-seat/player-seat.component';
import { CardFanComponent } from '../../shared/components/card-fan/card-fan.component';
import { CardComponent } from '../../shared/components/card/card.component';
import { GameModeBadgeComponent } from '../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../shared/components/multiplier-badge/multiplier-badge.component';
import { ReplayFrame, ReplayTimeline } from './replay-timeline';

export const REPLAY_STAGE_WIDTH = 540;
export const REPLAY_STAGE_HEIGHT = 960;

interface SeatView {
  position: PlayerPosition;
  relative: RelativePosition;
  name: string;
  isBot: boolean;
  team: Team;
}

@Component({
  selector: 'app-replay-stage',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslocoDirective,
    TrickAreaComponent,
    PlayerSeatComponent,
    CardFanComponent,
    CardComponent,
    GameModeBadgeComponent,
    MultiplierBadgeComponent,
  ],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div
      class="replay-stage"
      [class.render-mode]="renderMode()"
      [class.phase-intro]="frame().phase === 'intro'"
      [class.phase-outro]="frame().phase === 'outro'"
      *transloco="let t"
    >
      <header class="rs-header">
        <div class="rs-mode">
          <app-game-mode-badge [mode]="replay().gameMode" size="1.5rem" />
          @if (replay().multiplier !== 'Normal') {
            <app-multiplier-badge [multiplier]="replay().multiplier" />
          }
        </div>
        <div
          class="rs-points"
          [class.team1-lead]="frame().team1CardPoints > frame().team2CardPoints"
          [class.team2-lead]="frame().team2CardPoints > frame().team1CardPoints"
        >
          <span class="team1">{{ frame().team1CardPoints }}</span>
          <span class="sep">·</span>
          <span class="team2">{{ frame().team2CardPoints }}</span>
        </div>
        <div class="rs-deal">
          <span class="deal-label">{{
            t('replay.dealNumber', { number: replay().dealNumber })
          }}</span>
          <span class="match-score">
            <span class="team1">{{ replay().team1ScoreBefore }}</span> –
            <span class="team2">{{ replay().team2ScoreBefore }}</span>
          </span>
        </div>
      </header>

      <div class="rs-felt">
        <div class="rs-seat top">
          <ng-container *ngTemplateOutlet="seat; context: { $implicit: seatAt('top') }" />
          @if (showAllHands() && seatAt('top'); as s) {
            <div class="rs-mini-hand">
              @for (card of handOf(s.position); track card.rank + card.suit) {
                <app-card
                  [card]="card"
                  [faceUp]="true"
                  [gameMode]="replay().gameMode"
                  [width]="34"
                />
              }
            </div>
          }
        </div>

        <div class="rs-middle">
          <div class="rs-side left">
            <ng-container *ngTemplateOutlet="seat; context: { $implicit: seatAt('left') }" />
            @if (showAllHands() && seatAt('left'); as s) {
              <div class="rs-mini-hand vertical">
                @for (card of handOf(s.position); track card.rank + card.suit) {
                  <app-card
                    [card]="card"
                    [faceUp]="true"
                    [gameMode]="replay().gameMode"
                    [width]="30"
                  />
                }
              </div>
            }
          </div>

          <app-trick-area
            [currentTrick]="frame().trick"
            [entryProgress]="frame().entryProgress"
            [myPosition]="anchor()"
            [gameMode]="replay().gameMode"
            [isWatcher]="true"
          />

          <div class="rs-side right">
            <ng-container *ngTemplateOutlet="seat; context: { $implicit: seatAt('right') }" />
            @if (showAllHands() && seatAt('right'); as s) {
              <div class="rs-mini-hand vertical">
                @for (card of handOf(s.position); track card.rank + card.suit) {
                  <app-card
                    [card]="card"
                    [faceUp]="true"
                    [gameMode]="replay().gameMode"
                    [width]="30"
                  />
                }
              </div>
            }
          </div>
        </div>

        <div class="rs-seat bottom">
          <ng-container *ngTemplateOutlet="seat; context: { $implicit: seatAt('bottom') }" />
        </div>

        <div class="rs-hand">
          <app-card-fan
            [cards]="anchorHand()"
            [gameMode]="replay().gameMode"
            [interactive]="false"
          />
        </div>

        @if (frame().phase === 'intro') {
          <div class="rs-card intro">
            <div class="rs-card-title">{{ t('game.modes.' + modeKey()) }}</div>
            <div class="rs-card-teams">
              <span class="team1">{{ teamNames(Team1) }}</span>
              <span class="vs">{{ t('replay.vs') }}</span>
              <span class="team2">{{ teamNames(Team2) }}</span>
            </div>
          </div>
        }
        @if (frame().phase === 'outro') {
          <div class="rs-card outro">
            <div class="rs-card-title">
              @if (replay().wasSweep) {
                {{ t('replay.sweep') }}
              } @else {
                {{ t('replay.dealResult') }}
              }
            </div>
            <div class="rs-card-score">
              <span class="team1">{{ replay().team1CardPoints }}</span>
              <span class="sep">·</span>
              <span class="team2">{{ replay().team2CardPoints }}</span>
            </div>
            <div class="rs-card-match">
              <span class="team1">+{{ replay().team1MatchPointsEarned }}</span>
              <span class="label">{{ t('replay.matchPoints') }}</span>
              <span class="team2">+{{ replay().team2MatchPointsEarned }}</span>
            </div>
          </div>
        }
      </div>

      <footer class="rs-footer">
        <span class="rs-caption">{{ captionText(t) }}</span>
        <span class="rs-brand">giretra</span>
      </footer>

      <ng-template #seat let-s>
        @if (s) {
          <app-player-seat
            [position]="s.position"
            [playerName]="s.name"
            [isOccupied]="true"
            [isAi]="s.isBot"
            [isActiveTurn]="frame().activePlayer === s.position"
            [isDealer]="replay().dealer === s.position"
            [cardCount]="cardCountOf(s.position)"
            [tricksWon]="tricksWonOf(s.position)"
          />
        }
      </ng-template>
    </div>
  `,
  styles: [
    `
      .replay-stage {
        width: 540px;
        height: 960px;
        display: flex;
        flex-direction: column;
        background: hsl(var(--background));
        color: hsl(var(--foreground));
        font-family: 'Inter', system-ui, sans-serif;
        overflow: hidden;
        position: relative;
        -webkit-user-select: none;
        user-select: none;
      }
      /* Render mode: every frame must be a pure function of t. */
      .replay-stage.render-mode *,
      .replay-stage.render-mode *::before,
      .replay-stage.render-mode *::after {
        animation: none !important;
        transition: none !important;
        caret-color: transparent !important;
      }
      .rs-header,
      .rs-footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0 1.25rem;
        height: 80px;
        flex: 0 0 auto;
      }
      .rs-mode {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .rs-points {
        font-family: 'Urbanist', 'Inter', sans-serif;
        font-weight: 800;
        font-size: 2rem;
        display: flex;
        gap: 0.5rem;
        line-height: 1;
      }
      .replay-stage .team1 {
        color: hsl(var(--team1));
      }
      .replay-stage .team2 {
        color: hsl(var(--team2));
      }
      .replay-stage .sep {
        color: hsl(var(--muted-foreground));
      }
      .rs-deal {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 0.125rem;
      }
      .deal-label {
        color: hsl(var(--muted-foreground));
        font-size: 0.8rem;
      }
      .match-score {
        font-size: 0.9rem;
        font-weight: 600;
        color: hsl(var(--muted-foreground));
      }
      .rs-felt {
        position: relative;
        flex: 1 1 auto;
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        padding: 0.75rem;
        background: radial-gradient(
          ellipse at 50% 50%,
          hsl(var(--table-felt-light)),
          hsl(var(--table-felt)) 70%
        );
      }
      .rs-seat {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.375rem;
      }
      .rs-middle {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }
      .rs-side {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.375rem;
        width: 110px;
      }
      .rs-hand {
        display: flex;
        justify-content: center;
        min-height: 150px;
        align-items: flex-end;
      }
      .rs-mini-hand {
        display: flex;
        gap: 2px;
        justify-content: center;
        flex-wrap: wrap;
        max-width: 300px;
      }
      .rs-mini-hand.vertical {
        max-width: 100px;
      }
      .rs-mini-hand app-card {
        display: block;
      }
      .rs-caption {
        font-size: 1rem;
        font-weight: 500;
      }
      .rs-brand {
        font-family: 'Urbanist', sans-serif;
        font-weight: 800;
        color: hsl(var(--gold));
        letter-spacing: 0.05em;
      }
      .rs-card {
        position: absolute;
        inset: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 1rem;
        background: hsl(var(--background) / 0.78);
        text-align: center;
        padding: 2rem;
      }
      .rs-card-title {
        font-family: 'Urbanist', sans-serif;
        font-weight: 800;
        font-size: 2.25rem;
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }
      .rs-card-teams {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        font-size: 1.25rem;
        font-weight: 600;
      }
      .rs-card-teams .vs {
        color: hsl(var(--muted-foreground));
        font-size: 0.9rem;
        text-transform: uppercase;
      }
      .rs-card-score {
        font-family: 'Urbanist', sans-serif;
        font-weight: 800;
        font-size: 3.5rem;
        display: flex;
        gap: 0.75rem;
      }
      .rs-card-match {
        display: flex;
        gap: 0.75rem;
        font-size: 1.25rem;
        font-weight: 700;
        align-items: baseline;
      }
      .rs-card-match .label {
        color: hsl(var(--muted-foreground));
        font-size: 0.85rem;
        font-weight: 500;
      }
    `,
  ],
})
export class ReplayStageComponent {
  readonly replay = input.required<DealReplayResponse>();
  readonly timeline = input.required<ReplayTimeline>();
  /** Current time in ms. */
  readonly t = input.required<number>();
  /** Seat drawn at the bottom; its hand is shown face up. */
  readonly anchor = input<PlayerPosition>(PlayerPosition.Bottom);
  /** Also show the three other hands face up. */
  readonly showAllHands = input<boolean>(false);
  /** Headless rendering: animations off, deterministic frames. */
  readonly renderMode = input<boolean>(false);

  readonly Team1 = Team.Team1;
  readonly Team2 = Team.Team2;

  readonly frame = computed<ReplayFrame>(() => this.timeline().frameAt(this.t()));

  readonly seats = computed<SeatView[]>(() => {
    const anchor = this.anchor();
    return this.replay().players.map((p) => ({
      position: p.position,
      relative: toRelativePosition(p.position, anchor),
      name: p.displayName,
      isBot: p.isBot,
      team: p.team,
    }));
  });

  readonly anchorHand = computed<CardResponse[]>(() => this.frame().hands[this.anchor()] ?? []);

  readonly modeKey = computed(() => {
    switch (this.replay().gameMode) {
      case 'ColourClubs':
        return 'clubs';
      case 'ColourDiamonds':
        return 'diamonds';
      case 'ColourHearts':
        return 'hearts';
      case 'ColourSpades':
        return 'spades';
      case 'NoTrumps':
        return 'noTrumps';
      default:
        return 'allTrumps';
    }
  });

  cardCountOf(position: PlayerPosition): number {
    return this.frame().cardCounts[position] ?? 0;
  }

  tricksWonOf(position: PlayerPosition): number {
    return this.frame().tricksWon[position] ?? 0;
  }

  handOf(position: PlayerPosition): CardResponse[] {
    return this.frame().hands[position] ?? [];
  }

  seatAt(relative: RelativePosition): SeatView | null {
    return this.seats().find((s) => s.relative === relative) ?? null;
  }

  teamNames(team: Team): string {
    return this.replay()
      .players.filter((p) => getTeam(p.position) === team)
      .map((p) => p.displayName)
      .join(' & ');
  }

  playerName(position: PlayerPosition): string {
    return this.replay().players.find((p) => p.position === position)?.displayName ?? '';
  }

  captionText(t: (key: string, params?: Record<string, unknown>) => string): string {
    const caption = this.frame().caption;
    switch (caption.kind) {
      case 'intro':
        return t('replay.caption.intro', { deal: this.replay().dealNumber });
      case 'trick':
        return t('replay.caption.trick', { number: caption.trickNumber });
      case 'trickWon':
        return t('replay.caption.trickWon', {
          number: caption.trickNumber,
          name: this.playerName(caption.winner),
          points: caption.points,
        });
      case 'outro':
        return 'play.giretra.com';
    }
  }
}
