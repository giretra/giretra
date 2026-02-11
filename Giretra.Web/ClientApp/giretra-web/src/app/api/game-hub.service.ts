import { Injectable, NgZone, OnDestroy, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../core/services/auth.service';
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
  private readonly ngZone = inject(NgZone);
  private readonly auth = inject(AuthService);
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

    await this.hubConnection.start();
    console.log('[Hub] Connected successfully');
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
  }
}
