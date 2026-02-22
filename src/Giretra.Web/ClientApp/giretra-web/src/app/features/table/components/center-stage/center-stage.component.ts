import { Component, input, output } from '@angular/core';
import { GameMode, PlayerPosition, SeatAccessMode, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse, NegotiationAction, TrickResponse } from '../../../../core/services/api.service';
import { GamePhase } from '../../../../core/services/game-state.service';
import { WaitingStageComponent } from './waiting-stage/waiting-stage.component';
import { CutStageComponent } from './cut-stage/cut-stage.component';
import { NegotiationStageComponent } from './negotiation-stage/negotiation-stage.component';
import { TrickAreaComponent } from './trick-area/trick-area.component';
import { DealSummaryComponent } from './deal-summary/deal-summary.component';

@Component({
  selector: 'app-center-stage',
  standalone: true,
  imports: [
    WaitingStageComponent,
    CutStageComponent,
    NegotiationStageComponent,
    TrickAreaComponent,
    DealSummaryComponent,
  ],
  template: `
    <div class="center-stage">
      @switch (phase()) {
        @case ('waiting') {
          <app-waiting-stage
            [room]="room()"
            [isCreator]="isCreator()"
            [isWatcher]="isWatcher()"
            (startGame)="startGame.emit()"
            (setSeatMode)="setSeatMode.emit($event)"
            (generateInvite)="generateInvite.emit($event)"
            (kickPlayer)="kickPlayer.emit($event)"
          />
        }
        @case ('cut') {
          <app-cut-stage
            [activePlayer]="activePlayer()"
            [myPosition]="myPosition()"
            [isWatcher]="isWatcher()"
            (submitCut)="submitCut.emit()"
          />
        }
        @case ('negotiation') {
          <app-negotiation-stage
            [negotiationHistory]="negotiationHistory()"
            [activePlayer]="activePlayer()"
            [myPosition]="myPosition()"
            [gameMode]="gameMode()"
          />
        }
        @case ('playing') {
          <app-trick-area
            [currentTrick]="currentTrick()"
            [completedTrickToShow]="completedTrickToShow()"
            [showingCompletedTrick]="showingCompletedTrick()"
            [myPosition]="myPosition()"
            [gameMode]="gameMode()"
            (dismissCompletedTrick)="dismissCompletedTrick.emit()"
          />
        }
        @case ('dealSummary') {
          <app-deal-summary
            [summary]="dealSummary()"
            [myTeam]="myTeam()"
            (dismissed)="hideDealSummary.emit()"
          />
        }
      }
    </div>
  `,
  styles: [`
    .center-stage {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
    }
  `],
})
export class CenterStageComponent {
  readonly phase = input.required<GamePhase>();
  readonly room = input<RoomResponse | null>(null);
  readonly isCreator = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
  readonly currentTrick = input<TrickResponse | null>(null);
  readonly completedTrickToShow = input<TrickResponse | null>(null);
  readonly showingCompletedTrick = input<boolean>(false);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly gameMode = input<GameMode | null>(null);
  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly dealSummary = input<any>(null);
  readonly matchWinner = input<Team | null>(null);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly completedDeals = input<number>(0);
  readonly team1Tricks = input<number>(0);
  readonly team2Tricks = input<number>(0);
  readonly myTeam = input<Team | null>(null);

  readonly startGame = output<void>();
  readonly submitCut = output<void>();
  readonly hideDealSummary = output<void>();
  readonly dismissCompletedTrick = output<void>();
  readonly setSeatMode = output<{ position: PlayerPosition; accessMode: SeatAccessMode }>();
  readonly generateInvite = output<PlayerPosition>();
  readonly kickPlayer = output<PlayerPosition>();
}
