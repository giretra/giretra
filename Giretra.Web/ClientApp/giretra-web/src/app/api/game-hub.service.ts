import { Injectable, NgZone, OnDestroy, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../core/services/auth.service';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';
import {
  CardPlayedEvent,
  DealEndedEvent,
  DealStartedEvent,
  GameHubEventNames,
  GameStartedEvent,
  MatchEndedEvent,
  PlayerJoinedEvent,
  PlayerKickedEvent,
  PlayerLeftEvent,
  PlayerTurnEvent,
  SeatModeChangedEvent,
  TrickCompletedEvent,
  YourTurnEvent,
} from './generated/signalr-types.generated';

@Injectable({
  providedIn: 'root',
})
export class GameHubService implements OnDestroy {
  private readonly ngZone = inject(NgZone);
  private readonly auth = inject(AuthService);
  private hubConnection: HubConnection | null = null;

  // Connection status
  private readonly _connectionStatus = signal<ConnectionStatus>('disconnected');
  readonly connectionStatus = this._connectionStatus.asReadonly();
  readonly reconnected$ = new Subject<void>();

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
  readonly playerKicked$ = new Subject<PlayerKickedEvent>();
  readonly seatModeChanged$ = new Subject<SeatModeChangedEvent>();

  async connect(hubUrl: string): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      console.log('[Hub] Already connected');
      return;
    }

    console.log('[Hub] Connecting to', hubUrl);
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.getToken() })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.registerEventHandlers();
    this.registerConnectionHandlers();

    await this.hubConnection.start();
    this.ngZone.run(() => this._connectionStatus.set('connected'));
    console.log('[Hub] Connected successfully');
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
    }
    this._connectionStatus.set('disconnected');
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
    this.playerKicked$.complete();
    this.seatModeChanged$.complete();
  }

  private registerEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on(GameHubEventNames.PlayerJoined, (event: PlayerJoinedEvent) => {
      console.log('[Hub] PlayerJoined', event);
      this.ngZone.run(() => this.playerJoined$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerLeft, (event: PlayerLeftEvent) => {
      console.log('[Hub] PlayerLeft', event);
      this.ngZone.run(() => this.playerLeft$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.GameStarted, (event: GameStartedEvent) => {
      console.log('[Hub] GameStarted', event);
      this.ngZone.run(() => this.gameStarted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.DealStarted, (event: DealStartedEvent) => {
      console.log('[Hub] DealStarted', event);
      this.ngZone.run(() => this.dealStarted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.DealEnded, (event: DealEndedEvent) => {
      console.log('[Hub] DealEnded', event);
      this.ngZone.run(() => this.dealEnded$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.YourTurn, (event: YourTurnEvent) => {
      console.log('[Hub] YourTurn', event);
      this.ngZone.run(() => this.yourTurn$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerTurn, (event: PlayerTurnEvent) => {
      console.log('[Hub] PlayerTurn', event);
      this.ngZone.run(() => this.playerTurn$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.CardPlayed, (event: CardPlayedEvent) => {
      console.log('[Hub] CardPlayed', event);
      this.ngZone.run(() => this.cardPlayed$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.TrickCompleted, (event: TrickCompletedEvent) => {
      console.log('[Hub] TrickCompleted', event);
      this.ngZone.run(() => this.trickCompleted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.MatchEnded, (event: MatchEndedEvent) => {
      console.log('[Hub] MatchEnded', event);
      this.ngZone.run(() => this.matchEnded$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerKicked, (event: PlayerKickedEvent) => {
      console.log('[Hub] PlayerKicked', event);
      this.ngZone.run(() => this.playerKicked$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.SeatModeChanged, (event: SeatModeChangedEvent) => {
      console.log('[Hub] SeatModeChanged', event);
      this.ngZone.run(() => this.seatModeChanged$.next(event));
    });
  }
}
