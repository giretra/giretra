import { Component, input, output, computed } from '@angular/core';
import { GameMode, PlayerPosition, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse, NegotiationAction, TrickResponse } from '../../../../core/services/api.service';
import { GamePhase } from '../../../../core/services/game-state.service';
import { getRelativePositions } from '../../../../core/utils/position-utils';
import { PlayerSeatComponent } from '../../../../shared/components/player-seat/player-seat.component';
import { CenterStageComponent } from '../center-stage/center-stage.component';

@Component({
  selector: 'app-table-surface',
  standalone: true,
  imports: [PlayerSeatComponent, CenterStageComponent],
  template: `
    <div class="table-surface">
      <!-- Top player (across from me) -->
      <div class="seat-position top">
        @if (topSlot(); as slot) {
          <app-player-seat
            [position]="slot.position"
            [playerName]="slot.playerName"
            [isOccupied]="slot.isOccupied"
            [isAi]="slot.isAi"
            [isActiveTurn]="activePlayer() === slot.position"
            [isDealer]="dealer() === slot.position"
            [cardCount]="getCardCount(slot.position)"
          />
        }
      </div>

      <!-- Middle row: Left player, Center Stage, Right player -->
      <div class="middle-row">
        <!-- Left player -->
        <div class="seat-position left">
          @if (leftSlot(); as slot) {
            <app-player-seat
              [position]="slot.position"
              [playerName]="slot.playerName"
              [isOccupied]="slot.isOccupied"
              [isAi]="slot.isAi"
              [isActiveTurn]="activePlayer() === slot.position"
              [isDealer]="dealer() === slot.position"
              [cardCount]="getCardCount(slot.position)"
            />
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
            (startGame)="startGame.emit()"
            (submitCut)="submitCut.emit()"
            (hideDealSummary)="hideDealSummary.emit()"
            (dismissCompletedTrick)="dismissCompletedTrick.emit()"
          />
        </div>

        <!-- Right player -->
        <div class="seat-position right">
          @if (rightSlot(); as slot) {
            <app-player-seat
              [position]="slot.position"
              [playerName]="slot.playerName"
              [isOccupied]="slot.isOccupied"
              [isAi]="slot.isAi"
              [isActiveTurn]="activePlayer() === slot.position"
              [isDealer]="dealer() === slot.position"
              [cardCount]="getCardCount(slot.position)"
            />
          }
        </div>
      </div>

      <!-- Bottom player (me) -->
      <div class="seat-position bottom">
        @if (bottomSlot(); as slot) {
          <app-player-seat
            [position]="slot.position"
            [playerName]="slot.playerName"
            [isOccupied]="slot.isOccupied"
            [isAi]="slot.isAi"
            [isActiveTurn]="activePlayer() === slot.position"
            [isDealer]="dealer() === slot.position"
            [showCardBacks]="false"
          />
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
}
