import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { ApiService, RoomResponse } from '../../core/services/api.service';
import { ClientSessionService } from '../../core/services/client-session.service';
import { AuthService } from '../../core/services/auth.service';
import { GameStateService } from '../../core/services/game-state.service';
import { RoomListComponent } from './components/room-list/room-list.component';
import { CreateRoomFormComponent } from './components/create-room-form/create-room-form.component';
import { LucideAngularModule, Plus, LogOut } from 'lucide-angular';

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
                <button class="change-name-btn" (click)="logout()" title="Logout">
                  <i-lucide [img]="LogOutIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
              </div>
            }
          </div>
        </div>
      </header>

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
    .change-name-btn { display:flex; align-items:center; justify-content:center; width:1.5rem; height:1.5rem; border-radius:50%; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .change-name-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
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

  private readonly api = inject(ApiService);
  readonly session = inject(ClientSessionService);
  readonly auth = inject(AuthService);
  private readonly gameState = inject(GameStateService);
  private readonly router = inject(Router);

  private pollSubscription: Subscription | null = null;

  readonly rooms = signal<RoomResponse[]>([]);
  readonly loading = signal<boolean>(true);
  readonly showCreateForm = signal<boolean>(false);

  ngOnInit(): void {
    this.loadRooms();

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

  logout(): void {
    this.auth.logout();
  }

  onRoomCreated(room: RoomResponse): void {
    this.showCreateForm.set(false);
    this.navigateToTable(room, true);
  }

  onJoinRoom(room: RoomResponse): void {
    this.api.joinRoom(room.roomId).subscribe({
      next: (response) => {
        if (response.position) {
          this.session.joinRoom(room.roomId, response.clientId, response.position);
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

  private async navigateToTable(room: RoomResponse, isCreator: boolean): Promise<void> {
    await this.gameState.enterRoom(room, isCreator);
    this.router.navigate(['/table', room.roomId]);
  }
}
