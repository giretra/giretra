import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { ApiService, RoomResponse } from '../../core/services/api.service';
import { ClientSessionService } from '../../core/services/client-session.service';
import { GameStateService } from '../../core/services/game-state.service';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { RoomListComponent } from './components/room-list/room-list.component';
import { CreateRoomFormComponent } from './components/create-room-form/create-room-form.component';
import { NamePromptDialogComponent } from './components/name-prompt-dialog/name-prompt-dialog.component';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    RoomListComponent,
    CreateRoomFormComponent,
    NamePromptDialogComponent,
    HlmButton,
  ],
  template: `
    <!-- Name prompt dialog -->
    @if (showNamePrompt()) {
      <app-name-prompt-dialog
        (nameSubmitted)="onNameSubmitted($event)"
      />
    }

    <div class="home-container">
      <!-- Header -->
      <header class="header">
        <h1 class="logo">GIRETRA</h1>
        <div class="user-info">
          @if (session.hasName()) {
            <span class="player-name">{{ session.playerName() }}</span>
            <button hlmBtn variant="ghost" size="sm" (click)="changeName()">
              Change
            </button>
          }
        </div>
      </header>

      <!-- Main content -->
      <main class="main-content">
        <!-- Create room section -->
        <section class="create-section">
          @if (showCreateForm()) {
            <app-create-room-form
              [playerName]="session.playerName() ?? ''"
              (roomCreated)="onRoomCreated($event)"
              (cancelled)="showCreateForm.set(false)"
            />
          } @else {
            <button
              hlmBtn
              variant="default"
              class="create-button"
              (click)="showCreateForm.set(true)"
            >
              <span class="plus-icon">+</span>
              Create a new room
            </button>
          }
        </section>

        <!-- Room list section -->
        <section class="rooms-section">
          <h2 class="section-title">Available Rooms</h2>
          <app-room-list
            [rooms]="rooms()"
            [loading]="loading()"
            (joinRoom)="onJoinRoom($event)"
            (watchRoom)="onWatchRoom($event)"
          />
        </section>
      </main>
    </div>
  `,
  styles: [`
    .home-container {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      padding: 1rem;
      max-width: 800px;
      margin: 0 auto;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 0;
      border-bottom: 1px solid hsl(var(--border));
      margin-bottom: 2rem;
    }

    .logo {
      font-size: 1.5rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      color: hsl(var(--primary));
    }

    .user-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .player-name {
      color: hsl(var(--foreground));
      font-weight: 500;
    }

    .main-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }

    .create-section {
      width: 100%;
    }

    .create-button {
      width: 100%;
      padding: 1rem;
      font-size: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .plus-icon {
      font-size: 1.25rem;
      font-weight: 700;
    }

    .rooms-section {
      flex: 1;
    }

    .section-title {
      font-size: 1rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      margin-bottom: 1rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
  `],
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  readonly session = inject(ClientSessionService);
  private readonly gameState = inject(GameStateService);
  private readonly router = inject(Router);

  private pollSubscription: Subscription | null = null;

  readonly rooms = signal<RoomResponse[]>([]);
  readonly loading = signal<boolean>(true);
  readonly showCreateForm = signal<boolean>(false);
  readonly showNamePrompt = signal<boolean>(false);

  // Track pending actions
  private pendingJoinRoom: RoomResponse | null = null;
  private pendingWatchRoom: RoomResponse | null = null;

  ngOnInit(): void {
    // Check if name is set
    if (!this.session.hasName()) {
      this.showNamePrompt.set(true);
    }

    // Load rooms
    this.loadRooms();

    // Poll for room updates every 5 seconds
    this.pollSubscription = interval(5000).subscribe(() => {
      this.loadRooms();
    });
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  private loadRooms(): void {
    this.api.listRooms().subscribe({
      next: (response) => {
        this.rooms.set(response.rooms);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load rooms', err);
        this.loading.set(false);
      },
    });
  }

  onNameSubmitted(name: string): void {
    this.session.setPlayerName(name);
    this.showNamePrompt.set(false);

    // Process any pending actions
    if (this.pendingJoinRoom) {
      this.joinRoom(this.pendingJoinRoom);
      this.pendingJoinRoom = null;
    } else if (this.pendingWatchRoom) {
      this.watchRoom(this.pendingWatchRoom);
      this.pendingWatchRoom = null;
    }
  }

  changeName(): void {
    this.showNamePrompt.set(true);
  }

  onRoomCreated(room: RoomResponse): void {
    this.showCreateForm.set(false);
    this.navigateToTable(room, true);
  }

  onJoinRoom(room: RoomResponse): void {
    if (!this.session.hasName()) {
      this.pendingJoinRoom = room;
      this.showNamePrompt.set(true);
      return;
    }
    this.joinRoom(room);
  }

  onWatchRoom(room: RoomResponse): void {
    if (!this.session.hasName()) {
      this.pendingWatchRoom = room;
      this.showNamePrompt.set(true);
      return;
    }
    this.watchRoom(room);
  }

  private joinRoom(room: RoomResponse): void {
    const playerName = this.session.playerName();
    if (!playerName) return;

    this.api.joinRoom(room.roomId, playerName).subscribe({
      next: (response) => {
        if (response.position) {
          this.session.joinRoom(room.roomId, response.clientId, response.position);
          this.navigateToTable(response.room, false);
        }
      },
      error: (err) => {
        console.error('Failed to join room', err);
        // Could show a toast here
      },
    });
  }

  private watchRoom(room: RoomResponse): void {
    const playerName = this.session.playerName();
    if (!playerName) return;

    this.api.watchRoom(room.roomId, playerName).subscribe({
      next: (response) => {
        this.session.watchRoom(room.roomId, response.clientId);
        this.navigateToTable(response.room, false);
      },
      error: (err) => {
        console.error('Failed to watch room', err);
      },
    });
  }

  private async navigateToTable(room: RoomResponse, isCreator: boolean): Promise<void> {
    await this.gameState.enterRoom(room, isCreator);
    this.router.navigate(['/table', room.roomId]);
  }
}
