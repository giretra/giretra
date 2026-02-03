import { Injectable, OnDestroy } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import {
  CardPlayedEvent,
  DealEndedEvent,
  DealStartedEvent,
  GameHubEventNames,
  GameStartedEvent,
  MatchEndedEvent,
  PlayerJoinedEvent,
  PlayerLeftEvent,
  PlayerTurnEvent,
  TrickCompletedEvent,
  YourTurnEvent,
} from './generated/signalr-types.generated';

@Injectable({
  providedIn: 'root',
})
export class GameHubService implements OnDestroy {
  private hubConnection: HubConnection | null = null;

  // Event subjects
  readonly playerJoined$ = new Subject<PlayerJoinedEvent>();
  readonly playerLeft$ = new Subject<PlayerLeftEvent>();
  readonly gameStarted$ = new Subject<GameStartedEvent>();
  readonly dealStarted$ = new Subject<DealStartedEvent>();
  readonly dealEnded$ = new Subject<DealEndedEvent>();
  readonly yourTurn$ = new Subject<YourTurnEvent>();
  readonly playerTurn$ = new Subject<PlayerTurnEvent>();
  readonly cardPlayed$ = new Subject<CardPlayedEvent>();
  readonly trickCompleted$ = new Subject<TrickCompletedEvent>();
  readonly matchEnded$ = new Subject<MatchEndedEvent>();

  async connect(hubUrl: string): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.registerEventHandlers();

    await this.hubConnection.start();
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
    }
  }

  async joinRoom(roomId: string, clientId: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('JoinRoom', roomId, clientId);
  }

  async leaveRoom(roomId: string, clientId: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('LeaveRoom', roomId, clientId);
  }

  get connectionState(): HubConnectionState {
    return this.hubConnection?.state ?? HubConnectionState.Disconnected;
  }

  ngOnDestroy(): void {
    this.disconnect();
    this.playerJoined$.complete();
    this.playerLeft$.complete();
    this.gameStarted$.complete();
    this.dealStarted$.complete();
    this.dealEnded$.complete();
    this.yourTurn$.complete();
    this.playerTurn$.complete();
    this.cardPlayed$.complete();
    this.trickCompleted$.complete();
    this.matchEnded$.complete();
  }

  private registerEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on(GameHubEventNames.PlayerJoined, (event: PlayerJoinedEvent) => {
      this.playerJoined$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.PlayerLeft, (event: PlayerLeftEvent) => {
      this.playerLeft$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.GameStarted, (event: GameStartedEvent) => {
      this.gameStarted$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.DealStarted, (event: DealStartedEvent) => {
      this.dealStarted$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.DealEnded, (event: DealEndedEvent) => {
      this.dealEnded$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.YourTurn, (event: YourTurnEvent) => {
      this.yourTurn$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.PlayerTurn, (event: PlayerTurnEvent) => {
      this.playerTurn$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.CardPlayed, (event: CardPlayedEvent) => {
      this.cardPlayed$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.TrickCompleted, (event: TrickCompletedEvent) => {
      this.trickCompleted$.next(event);
    });

    this.hubConnection.on(GameHubEventNames.MatchEnded, (event: MatchEndedEvent) => {
      this.matchEnded$.next(event);
    });
  }
}
