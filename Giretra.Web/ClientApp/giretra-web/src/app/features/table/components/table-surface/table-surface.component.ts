import { Component, input, output, computed } from '@angular/core';
import { GameMode, PlayerPosition, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse, NegotiationAction, TrickResponse } from '../../../../core/services/api.service';
import { GamePhase } from '../../../../core/services/game-state.service';
import { getRelativePositions } from '../../../../core/utils/position-utils';
import { PlayerSeatComponent } from '../../../../shared/components/player-seat/player-seat.component';
import { CenterStageComponent } from '../center-stage/center-stage.component';
import { SpeechBubbleComponent } from '../speech-bubble/speech-bubble.component';

@Component({
  selector: 'app-table-surface',
  standalone: true,
  imports: [PlayerSeatComponent, CenterStageComponent, SpeechBubbleComponent],
  template: `
    <div class="table-surface">
      <!-- Top player (across from me) -->
      <div class="seat-position top">
        @if (topSlot(); as slot) {
          <div class="seat-with-bubble">
            <app-player-seat
              [position]="slot.position"
              [playerName]="slot.playerName"
              [isOccupied]="slot.isOccupied"
              [isAi]="slot.isAi"
              [isActiveTurn]="activePlayer() === slot.position"
              [isDealer]="dealer() === slot.position"
              [cardCount]="getCardCount(slot.position)"
              [tricksWon]="getTricksWon(slot.position)"
            />
            @if (phase() === 'negotiation' && getLastAction(slot.position); as action) {
              <div class="bubble-position top">
                <app-speech-bubble
                  [actionType]="action.actionType"
                  [mode]="action.mode"
                  position="top"
                />
              </div>
            }
          </div>
        }
      </div>

      <!-- Middle row: Left player, Center Stage, Right player -->
      <div class="middle-row">
        <!-- Left player -->
        <div class="seat-position left">
          @if (leftSlot(); as slot) {
            <div class="seat-with-bubble horizontal">
              @if (phase() === 'negotiation' && getLastAction(slot.position); as action) {
                <div class="bubble-position left">
                  <app-speech-bubble
                    [actionType]="action.actionType"
                    [mode]="action.mode"
                    position="left"
                  />
                </div>
              }
              <app-player-seat
                [position]="slot.position"
                [playerName]="slot.playerName"
                [isOccupied]="slot.isOccupied"
                [isAi]="slot.isAi"
                [isActiveTurn]="activePlayer() === slot.position"
                [isDealer]="dealer() === slot.position"
                [cardCount]="getCardCount(slot.position)"
                [tricksWon]="getTricksWon(slot.position)"
              />
            </div>
          }
        </div>

        <!-- Center Stage -->
        <div class="center-stage-container">
          <app-center-stage
            [phase]="phase()"
            [room]="room()"
            [isCreator]="isCreator()"
            [isWatcher]="isWatcher()"
            [currentTrick]="currentTrick()"
            [completedTrickToShow]="completedTrickToShow()"
            [showingCompletedTrick]="showingCompletedTrick()"
            [myPosition]="myPosition()"
            [gameMode]="gameMode()"
            [negotiationHistory]="negotiationHistory()"
            [activePlayer]="activePlayer()"
            [dealSummary]="dealSummary()"
            [matchWinner]="matchWinner()"
            [team1MatchPoints]="team1MatchPoints()"
            [team2MatchPoints]="team2MatchPoints()"
            [completedDeals]="completedDeals()"
            [team1Tricks]="team1Tricks()"
            [team2Tricks]="team2Tricks()"
            [myTeam]="myTeam()"
            (startGame)="startGame.emit()"
            (submitCut)="submitCut.emit()"
            (hideDealSummary)="hideDealSummary.emit()"
            (dismissCompletedTrick)="dismissCompletedTrick.emit()"
          />
        </div>

        <!-- Right player -->
        <div class="seat-position right">
          @if (rightSlot(); as slot) {
            <div class="seat-with-bubble horizontal">
              <app-player-seat
                [position]="slot.position"
                [playerName]="slot.playerName"
                [isOccupied]="slot.isOccupied"
                [isAi]="slot.isAi"
                [isActiveTurn]="activePlayer() === slot.position"
                [isDealer]="dealer() === slot.position"
                [cardCount]="getCardCount(slot.position)"
                [tricksWon]="getTricksWon(slot.position)"
              />
              @if (phase() === 'negotiation' && getLastAction(slot.position); as action) {
                <div class="bubble-position right">
                  <app-speech-bubble
                    [actionType]="action.actionType"
                    [mode]="action.mode"
                    position="right"
                  />
                </div>
              }
            </div>
          }
        </div>
      </div>

      <!-- Bottom player (me) -->
      <div class="seat-position bottom">
        @if (bottomSlot(); as slot) {
          <div class="seat-with-bubble">
            @if (phase() === 'negotiation' && getLastAction(slot.position); as action) {
              <div class="bubble-position bottom">
                <app-speech-bubble
                  [actionType]="action.actionType"
                  [mode]="action.mode"
                  position="bottom"
                />
              </div>
            }
            <app-player-seat
              [position]="slot.position"
              [playerName]="slot.playerName"
              [isOccupied]="slot.isOccupied"
              [isAi]="slot.isAi"
              [isActiveTurn]="activePlayer() === slot.position"
              [isDealer]="dealer() === slot.position"
              [showCardBacks]="false"
              [tricksWon]="getTricksWon(slot.position)"
            />
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .table-surface {
      flex: 1;
      display: flex;
      flex-direction: column;
      padding: 0.5rem;
      min-height: 0;
      overflow: hidden;
    }

    .seat-position {
      display: flex;
      justify-content: center;
      padding: 0.25rem;
    }

    .seat-position.top,
    .seat-position.bottom {
      flex-shrink: 0;
    }

    .middle-row {
      flex: 1;
      display: flex;
      align-items: center;
      min-height: 0;
    }

    .middle-row .seat-position {
      flex-shrink: 0;
    }

    .center-stage-container {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      min-width: 0;
      padding: 0 0.5rem;
    }

    /* Seat with speech bubble container */
    .seat-with-bubble {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
    }

    .seat-with-bubble.horizontal {
      flex-direction: row;
    }

    .bubble-position {
      flex-shrink: 0;
    }

    /* Hide bubbles on very small screens */
    @media (max-width: 400px) {
      .bubble-position {
        display: none;
      }
    }
  `],
})
export class TableSurfaceComponent {
  readonly room = input<RoomResponse | null>(null);
  readonly phase = input.required<GamePhase>();
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly currentTrick = input<TrickResponse | null>(null);
  readonly completedTrickToShow = input<TrickResponse | null>(null);
  readonly showingCompletedTrick = input<boolean>(false);
  readonly gameMode = input<GameMode | null>(null);
  readonly dealer = input<PlayerPosition | null>(null);
  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly isCreator = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
  readonly dealSummary = input<any>(null);
  readonly matchWinner = input<Team | null>(null);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly completedDeals = input<number>(0);
  readonly playerCardCounts = input<Record<PlayerPosition, number> | null>(null);
  readonly team1Tricks = input<number>(0);
  readonly team2Tricks = input<number>(0);
  readonly myTeam = input<Team | null>(null);
  readonly tricksWonByPosition = input<Record<PlayerPosition, number> | null>(null);

  readonly startGame = output<void>();
  readonly submitCut = output<void>();
  readonly hideDealSummary = output<void>();
  readonly dismissCompletedTrick = output<void>();

  // Get slots relative to my position (for proper table layout)
  readonly relativePositions = computed(() => {
    const pos = this.myPosition();
    if (!pos) {
      // Default to Bottom perspective
      return getRelativePositions(PlayerPosition.Bottom);
    }
    return getRelativePositions(pos);
  });

  private getSlotByPosition(position: PlayerPosition) {
    return this.room()?.playerSlots?.find((s) => s.position === position) ?? null;
  }

  readonly topSlot = computed(() => {
    return this.getSlotByPosition(this.relativePositions().across);
  });

  readonly leftSlot = computed(() => {
    return this.getSlotByPosition(this.relativePositions().left);
  });

  readonly rightSlot = computed(() => {
    return this.getSlotByPosition(this.relativePositions().right);
  });

  readonly bottomSlot = computed(() => {
    return this.getSlotByPosition(this.relativePositions().self);
  });

  getCardCount(position: PlayerPosition): number {
    const counts = this.playerCardCounts();
    return counts?.[position] ?? 0;
  }

  getTricksWon(position: PlayerPosition): number {
    const counts = this.tricksWonByPosition();
    return counts?.[position] ?? 0;
  }

  /** Get the last negotiation action for a player */
  getLastAction(position: PlayerPosition): NegotiationAction | null {
    const history = this.negotiationHistory();
    // Find the last action by this player
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].player === position) {
        return history[i];
      }
    }
    return null;
  }
}
