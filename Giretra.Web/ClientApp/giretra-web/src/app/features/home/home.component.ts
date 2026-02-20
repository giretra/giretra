import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { ApiService, RoomResponse } from '../../core/services/api.service';
import { JoinRoomEvent } from './components/room-list/room-list.component';
import { ClientSessionService } from '../../core/services/client-session.service';
import { AuthService } from '../../core/services/auth.service';
import { GameStateService } from '../../core/services/game-state.service';
import { RoomListComponent } from './components/room-list/room-list.component';
import { CreateRoomFormComponent } from './components/create-room-form/create-room-form.component';
import { LucideAngularModule, Plus, LogOut, Settings, Trophy } from 'lucide-angular';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    RoomListComponent,
    CreateRoomFormComponent,
    LucideAngularModule,
  ],
  template: `
    <div class="home-shell">
      <!-- Hero header with felt texture -->
      <header class="hero">
        <div class="hero-felt"></div>
        <div class="hero-vignette"></div>
        <div class="hero-content">
          <div class="brand">
            <div class="suit-row">
              <span class="suit spade">\u2660</span>
              <span class="suit heart">\u2665</span>
              <span class="suit diamond">\u2666</span>
              <span class="suit club">\u2663</span>
            </div>
            <h1 class="logo">GIRETRA</h1>
            <p class="tagline">Belote Malagasy</p>
          </div>

          <!-- User greeting / name area -->
          <div class="user-area">
            @if (auth.user(); as user) {
              <div class="user-pill">
                <span class="user-avatar">{{ user.displayName.charAt(0).toUpperCase() }}</span>
                <span class="user-name">{{ user.displayName }}</span>
                <button class="pill-btn" (click)="goToLeaderboard()" title="Leaderboard">
                  <i-lucide [img]="TrophyIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
                <button class="pill-btn settings-btn" (click)="goToSettings()" title="Settings">
                  <i-lucide [img]="SettingsIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  @if (pendingFriendCount() > 0) {
                    <span class="badge-dot"></span>
                  }
                </button>
                <button class="pill-btn" (click)="logout()" title="Logout">
                  <i-lucide [img]="LogOutIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
              </div>
            }
          </div>
        </div>
      </header>

      <!-- Resume game banner -->
      @if (activeGameRoomId()) {
        <div class="resume-banner" (click)="resumeGame()">
          <span class="resume-text">You have a game in progress</span>
          <button class="resume-btn">Resume</button>
        </div>
      }

      <!-- Main body -->
      <main class="main">
        <div class="main-inner">
          <!-- Create room panel -->
          <section class="panel create-panel">
            @if (showCreateForm()) {
              <app-create-room-form
                (roomCreated)="onRoomCreated($event)"
                (cancelled)="showCreateForm.set(false)"
              />
            } @else {
              <button
                class="create-btn"
                (click)="showCreateForm.set(true)"
              >
                <span class="create-btn-icon">
                  <i-lucide [img]="PlusIcon" [size]="22" [strokeWidth]="2.5"></i-lucide>
                </span>
                <span class="create-btn-text">
                  <span class="create-btn-label">Create Room</span>
                  <span class="create-btn-hint">Start a new game table</span>
                </span>
              </button>
            }
          </section>

          <!-- Room list section -->
          <section class="panel rooms-panel">
            <div class="panel-header">
              <h2 class="panel-title">Open Tables</h2>
              <span class="room-count-badge">{{ rooms().length }}</span>
            </div>
            <app-room-list
              [rooms]="rooms()"
              [loading]="loading()"
              (joinRoom)="onJoinRoom($event)"
              (watchRoom)="onWatchRoom($event)"
            />
          </section>
        </div>
      </main>

    </div>
  `,
  styles: [`
    .home-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }
    .hero { position:relative; overflow:hidden; padding:2.5rem 1rem 2rem; flex-shrink:0; }
    .hero-felt { position:absolute; inset:0; background:radial-gradient(ellipse at 50% 100%,hsl(var(--table-felt-light)),hsl(var(--table-felt)) 70%); }
    .hero-vignette { position:absolute; inset:0; pointer-events:none; background:radial-gradient(ellipse at 50% 50%,transparent 30%,rgba(0,0,0,0.35) 100%); }
    .hero-content { position:relative; z-index:1; max-width:720px; margin:0 auto; display:flex; justify-content:space-between; align-items:center; }
    .brand { display:flex; flex-direction:column; gap:0.25rem; }
    .suit-row { display:flex; gap:0.5rem; font-size:0.875rem; }
    .suit { opacity:0.5; }
    .suit.heart,.suit.diamond { color:hsl(0 65% 55%); }
    .suit.spade,.suit.club { color:hsl(var(--foreground)); }
    .logo { font-size:2rem; font-weight:800; letter-spacing:0.15em; color:hsl(var(--foreground)); margin:0; line-height:1; text-shadow:0 2px 8px rgba(0,0,0,0.3); }
    .tagline { margin:0; font-size:0.75rem; font-weight:500; letter-spacing:0.2em; text-transform:uppercase; color:hsl(var(--gold)); opacity:0.8; }
    .user-area { display:flex; align-items:center; }
    .user-pill { display:flex; align-items:center; gap:0.5rem; background:hsl(var(--background)/0.4); backdrop-filter:blur(8px); border:1px solid hsl(var(--foreground)/0.1); border-radius:9999px; padding:0.25rem 0.5rem 0.25rem 0.25rem; }
    .user-avatar { width:2rem; height:2rem; border-radius:50%; background:hsl(var(--primary)/0.25); border:2px solid hsl(var(--primary)); display:flex; align-items:center; justify-content:center; font-size:0.875rem; font-weight:700; color:hsl(var(--primary)); text-transform:uppercase; }
    .user-name { font-size:0.875rem; font-weight:600; color:hsl(var(--foreground)); }
    .pill-btn { position:relative; display:flex; align-items:center; justify-content:center; width:1.5rem; height:1.5rem; border-radius:50%; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .pill-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .badge-dot { position:absolute; top:0; right:0; width:0.5rem; height:0.5rem; border-radius:50%; background:hsl(var(--destructive)); border:1.5px solid hsl(var(--background)/0.6); }
    .main { flex:1; padding:1.5rem 1rem; }
    .main-inner { max-width:720px; margin:0 auto; display:flex; flex-direction:column; gap:1.5rem; }
    .panel { width:100%; }
    .create-btn { width:100%; display:flex; align-items:center; gap:1rem; padding:1rem 1.25rem; background:hsl(var(--card)); border:1px dashed hsl(var(--primary)/0.4); border-radius:0.75rem; cursor:pointer; transition:all 0.15s ease; text-align:left; color:inherit; }
    .create-btn:hover { border-color:hsl(var(--primary)); border-style:solid; background:hsl(var(--primary)/0.06); transform:translateY(-1px); box-shadow:0 4px 16px rgba(0,0,0,0.12); }
    .create-btn:active { transform:translateY(0); }
    .create-btn-icon { display:flex; align-items:center; justify-content:center; width:2.75rem; height:2.75rem; border-radius:0.625rem; background:hsl(var(--primary)/0.15); color:hsl(var(--primary)); flex-shrink:0; }
    .create-btn-text { display:flex; flex-direction:column; gap:0.125rem; }
    .create-btn-label { font-size:1rem; font-weight:600; color:hsl(var(--foreground)); }
    .create-btn-hint { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .panel-header { display:flex; align-items:center; gap:0.5rem; margin-bottom:1rem; }
    .panel-title { font-size:0.8125rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.08em; margin:0; }
    .room-count-badge { font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.5rem; border-radius:9999px; min-width:1.25rem; text-align:center; }
    @media (min-width:640px) {
      .hero { padding:3rem 2rem 2.5rem; }
      .logo { font-size:2.5rem; }
      .main { padding:2rem; }
    }
    .resume-banner { display:flex; align-items:center; justify-content:center; gap:0.75rem; padding:0.625rem 1rem; background:hsl(var(--primary)/0.1); border-bottom:1px solid hsl(var(--primary)/0.25); cursor:pointer; transition:background 0.15s ease; }
    .resume-banner:hover { background:hsl(var(--primary)/0.15); }
    .resume-text { font-size:0.8125rem; font-weight:500; color:hsl(var(--primary)); }
    .resume-btn { padding:0.25rem 0.75rem; font-size:0.75rem; font-weight:600; background:hsl(var(--primary)); color:hsl(var(--primary-foreground)); border:none; border-radius:9999px; cursor:pointer; transition:opacity 0.15s ease; }
    .resume-btn:hover { opacity:0.85; }
    @media (max-width:480px) {
      .hero { padding:1.5rem 1rem; }
      .hero-content { flex-direction:column; align-items:flex-start; gap:1rem; }
      .logo { font-size:1.75rem; }
    }
  `],
})
export class HomeComponent implements OnInit, OnDestroy {
  readonly PlusIcon = Plus;
  readonly LogOutIcon = LogOut;
  readonly SettingsIcon = Settings;
  readonly TrophyIcon = Trophy;

  private readonly api = inject(ApiService);
  readonly session = inject(ClientSessionService);
  readonly auth = inject(AuthService);
  private readonly gameState = inject(GameStateService);
  private readonly router = inject(Router);

  private pollSubscription: Subscription | null = null;
  private friendPollSubscription: Subscription | null = null;

  readonly rooms = signal<RoomResponse[]>([]);
  readonly loading = signal<boolean>(true);
  readonly showCreateForm = signal<boolean>(false);
  readonly pendingFriendCount = signal<number>(0);
  readonly activeGameRoomId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadRooms();
    this.loadPendingFriendCount();
    this.checkActiveSession();

    this.pollSubscription = interval(5000).subscribe(() => {
      this.loadRooms();
    });

    this.friendPollSubscription = interval(30000).subscribe(() => {
      this.loadPendingFriendCount();
    });
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.friendPollSubscription?.unsubscribe();
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

  private loadPendingFriendCount(): void {
    this.api.getPendingFriendCount().subscribe({
      next: (res) => this.pendingFriendCount.set(res.count),
    });
  }

  goToLeaderboard(): void {
    this.router.navigate(['/leaderboard']);
  }

  goToSettings(): void {
    this.router.navigate(['/settings']);
  }

  logout(): void {
    this.auth.logout();
  }

  onRoomCreated(room: RoomResponse): void {
    this.showCreateForm.set(false);
    this.navigateToTable(room, true);
  }

  onJoinRoom(event: JoinRoomEvent): void {
    this.api.joinRoom(event.room.roomId, event.position).subscribe({
      next: (response) => {
        if (response.position) {
          this.session.joinRoom(event.room.roomId, response.clientId, response.position);
          this.navigateToTable(response.room, false);
        }
      },
      error: (err) => {
        console.error('Failed to join room', err);
      },
    });
  }

  onWatchRoom(room: RoomResponse): void {
    this.api.watchRoom(room.roomId).subscribe({
      next: (response) => {
        this.session.watchRoom(room.roomId, response.clientId);
        this.navigateToTable(response.room, false);
      },
      error: (err) => {
        console.error('Failed to watch room', err);
      },
    });
  }

  resumeGame(): void {
    const roomId = this.activeGameRoomId();
    if (roomId) {
      this.router.navigate(['/table', roomId]);
    }
  }

  private checkActiveSession(): void {
    const roomId = this.session.roomId();
    if (!roomId) return;

    // Validate the session is still live
    this.api.getRoom(roomId).subscribe({
      next: (room) => {
        if (room.status === 'Playing') {
          this.activeGameRoomId.set(roomId);
        } else {
          // Room is no longer active — clear stale session
          this.session.leaveRoom();
        }
      },
      error: () => {
        // Room not found — clear stale session
        this.session.leaveRoom();
      },
    });
  }

  private async navigateToTable(room: RoomResponse, isCreator: boolean): Promise<void> {
    await this.gameState.enterRoom(room, isCreator);
    this.router.navigate(['/table', room.roomId]);
  }
}
