import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { interval, Subscription } from 'rxjs';
import { ApiService, RoomResponse, AiTypeInfo, AiSeat } from '../../core/services/api.service';
import { JoinRoomEvent } from './components/room-list/room-list.component';
import { ClientSessionService } from '../../core/services/client-session.service';
import { AuthService } from '../../core/services/auth.service';
import { GameStateService } from '../../core/services/game-state.service';
import { RoomListComponent } from './components/room-list/room-list.component';
import { CreateRoomFormComponent } from './components/create-room-form/create-room-form.component';
import { LucideAngularModule, Plus, LogOut, Settings, Trophy, Github, Share2, Zap, Bot } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { HotToastService } from '@ngxpert/hot-toast';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';
import { QuickGameDialogComponent } from './components/quick-game-dialog/quick-game-dialog.component';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    RoomListComponent,
    CreateRoomFormComponent,
    LucideAngularModule,
    TranslocoDirective,
    LanguageSwitcherComponent,
    QuickGameDialogComponent,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="home-shell">
      <!-- Hero header with felt texture -->
      <header class="hero">
        <div class="hero-felt"></div>
        <div class="hero-content">
          <div class="brand">
            <img src="icon-192x192.png" alt="Giretra" class="hero-icon" width="28" height="28" />
            <h1 class="logo">giretra</h1>
          </div>

          <!-- User greeting / name area -->
          <div class="user-area">
            @if (auth.user(); as user) {
              <div class="user-pill">
                <span class="user-avatar" (click)="goToSettings()">{{ user.displayName.charAt(0).toUpperCase() }}</span>
                <span class="user-name" (click)="goToSettings()">{{ user.displayName }}</span>
                <app-language-switcher />
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
          <span class="resume-text">{{ t('home.resumeBanner') }}</span>
          <button class="resume-btn">{{ t('home.resume') }}</button>
        </div>
      }

      <!-- Main body -->
      <main class="main">
        <div class="main-inner">
          <!-- Quick Game -->
          <section class="panel">
            <button class="quick-game-btn" (click)="showQuickGame.set(true)">
              <span class="quick-game-icon">
                <i-lucide [img]="ZapIcon" [size]="22" [strokeWidth]="2.5"></i-lucide>
              </span>
              <span class="quick-game-text">
                <span class="quick-game-label">{{ t('quickGame.title') }}</span>
                <span class="quick-game-hint">{{ t('quickGame.subtitle') }}</span>
              </span>
              <span class="quick-game-arrow">
                <i-lucide [img]="BotIcon" [size]="18" [strokeWidth]="2"></i-lucide>
              </span>
            </button>
          </section>

          <!-- Quick Game Dialog -->
          <app-quick-game-dialog
            [open]="showQuickGame()"
            [aiTypes]="aiTypes()"
            (play)="quickGame($event)"
            (closed)="showQuickGame.set(false)"
            (createRoom)="showQuickGame.set(false); showCreateForm.set(true)"
          />

          <!-- Create room panel -->
          <section class="panel create-panel">
            @if (showCreateForm()) {
              <app-create-room-form
                (roomCreated)="onRoomCreated($event)"
                (cancelled)="showCreateForm.set(false)"
              />
            } @else {
              <div class="create-actions">
                <button
                  class="create-btn"
                  (click)="showCreateForm.set(true)"
                >
                  <span class="create-btn-icon">
                    <i-lucide [img]="PlusIcon" [size]="22" [strokeWidth]="2.5"></i-lucide>
                  </span>
                  <span class="create-btn-text">
                    <span class="create-btn-label">{{ t('home.createRoom') }}</span>
                    <span class="create-btn-hint">{{ t('home.createRoomHint') }}</span>
                  </span>
                </button>
                <button
                  class="invite-btn"
                  (click)="inviteFriends()"
                >
                  <span class="invite-btn-icon">
                    <i-lucide [img]="Share2Icon" [size]="18" [strokeWidth]="2"></i-lucide>
                  </span>
                  <span class="invite-btn-text">
                    <span class="invite-btn-label">{{ t('home.inviteFriends') }}</span>
                    <span class="invite-btn-hint">{{ t('home.inviteFriendsHint') }}</span>
                  </span>
                </button>
              </div>
            }
          </section>

          <!-- Room list section -->
          <section class="panel rooms-panel">
            <div class="panel-header">
              <h2 class="panel-title">{{ t('home.openTables') }}</h2>
              <span class="room-count-badge">{{ rooms().length }}</span>
            </div>
            <app-room-list
              [rooms]="rooms()"
              [loading]="loading()"
              (joinRoom)="onJoinRoom($event)"
              (watchRoom)="onWatchRoom($event)"
              (rejoinRoom)="onRejoinRoom($event)"
            />
          </section>
        </div>
      </main>

      <!-- Footer -->
      <footer class="footer">
        <div class="footer-inner">
          <div class="footer-links">
            <a href="https://www.giretra.com" target="_blank" rel="noopener noreferrer" class="footer-link">Website</a>
            <span class="footer-dot"></span>
            <a href="https://github.com/giretra" target="_blank" rel="noopener noreferrer" class="footer-link footer-link-icon"><i-lucide [img]="GithubIcon" [size]="12" [strokeWidth]="2"></i-lucide> Source Code</a>
            <span class="footer-dot"></span>
            <a class="best-player-link" (click)="goToLeaderboard()">
              <i-lucide [img]="TrophyIcon" [size]="12" [strokeWidth]="2"></i-lucide>
              {{ t('home.bestPlayers') }}
            </a>
          </div>
          <span class="footer-copy">&copy; {{ currentYear }} Giretra</span>
        </div>
      </footer>

    </div>
    </ng-container>
  `,
  styles: [`
    .home-shell { min-height:100vh; display:flex; flex-direction:column; background:hsl(var(--background)); }
    .hero { position:relative; overflow:hidden; padding:0 1rem; height:48px; display:flex; align-items:center; flex-shrink:0; }
    .hero-felt { position:absolute; inset:0; background:radial-gradient(ellipse at 50% 100%,hsl(var(--table-felt-light)),hsl(var(--table-felt)) 70%); }
    .hero-content { position:relative; z-index:1; max-width:960px; width:100%; margin:0 auto; display:flex; justify-content:space-between; align-items:center; height:100%; }
    .brand { display:flex; align-items:center; gap:0.5rem; }
    .hero-icon { width:1.5rem; height:1.5rem; flex-shrink:0; filter:drop-shadow(0 1px 4px rgba(0,0,0,0.3)); }
    .logo { font-family:'Urbanist',sans-serif; font-size:1.125rem; font-weight:800; letter-spacing:0.05em; color:hsl(var(--foreground)); margin:0; line-height:1; text-shadow:0 1px 4px rgba(0,0,0,0.3); }
    .user-area { display:flex; align-items:center; }
    .user-pill { display:flex; align-items:center; gap:0.375rem; background:hsl(var(--background)/0.4); backdrop-filter:blur(8px); border:1px solid hsl(var(--foreground)/0.1); border-radius:9999px; padding:0.2rem 0.4rem 0.2rem 0.2rem; }
    .user-avatar { width:1.5rem; height:1.5rem; border-radius:50%; background:hsl(var(--primary)/0.25); border:2px solid hsl(var(--primary)); display:flex; align-items:center; justify-content:center; font-size:0.6875rem; font-weight:700; color:hsl(var(--primary)); text-transform:uppercase; cursor:pointer; }
    .user-name { font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); cursor:pointer; }
    .pill-btn { position:relative; display:flex; align-items:center; justify-content:center; width:1.5rem; height:1.5rem; border-radius:50%; border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .pill-btn:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.1); }
    .badge-dot { position:absolute; top:0; right:0; width:0.5rem; height:0.5rem; border-radius:50%; background:hsl(var(--destructive)); border:1.5px solid hsl(var(--background)/0.6); }
    .main { flex:1; padding:1.5rem 1rem; }
    .main-inner { max-width:960px; margin:0 auto; display:flex; flex-direction:column; gap:1.5rem; }
    .panel { width:100%; }
    .quick-game-btn { width:100%; display:flex; align-items:center; gap:1rem; padding:1rem 1.25rem; background:hsl(var(--gold)/0.08); border:2px solid hsl(var(--gold)/0.35); border-radius:0.75rem; cursor:pointer; transition:all 0.15s ease; text-align:left; color:inherit; }
    .quick-game-btn:hover { border-color:hsl(var(--gold)/0.7); background:hsl(var(--gold)/0.12); transform:translateY(-1px); box-shadow:0 4px 20px hsl(var(--gold)/0.15); }
    .quick-game-btn:active { transform:translateY(0); }
    .quick-game-icon { display:flex; align-items:center; justify-content:center; width:2.75rem; height:2.75rem; border-radius:0.625rem; background:hsl(var(--gold)/0.2); color:hsl(var(--gold)); flex-shrink:0; }
    .quick-game-text { display:flex; flex-direction:column; gap:0.125rem; flex:1; }
    .quick-game-label { font-size:1.0625rem; font-weight:700; color:hsl(var(--gold)); }
    .quick-game-hint { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .quick-game-arrow { color:hsl(var(--gold)/0.5); flex-shrink:0; }
    .create-actions { display:flex; flex-direction:column; gap:0.625rem; }
    @media (min-width:540px) { .create-actions { flex-direction:row; } }
    .create-btn { flex:1; display:flex; align-items:center; gap:1rem; padding:1rem 1.25rem; background:hsl(var(--card)); border:1px dashed hsl(var(--primary)/0.4); border-radius:0.75rem; cursor:pointer; transition:all 0.15s ease; text-align:left; color:inherit; }
    .create-btn:hover { border-color:hsl(var(--primary)); border-style:solid; background:hsl(var(--primary)/0.06); transform:translateY(-1px); box-shadow:0 4px 16px rgba(0,0,0,0.12); }
    .create-btn:active { transform:translateY(0); }
    .create-btn-icon { display:flex; align-items:center; justify-content:center; width:2.75rem; height:2.75rem; border-radius:0.625rem; background:hsl(var(--primary)/0.15); color:hsl(var(--primary)); flex-shrink:0; }
    .create-btn-text { display:flex; flex-direction:column; gap:0.125rem; }
    .create-btn-label { font-size:1rem; font-weight:600; color:hsl(var(--foreground)); }
    .create-btn-hint { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .invite-btn { flex:1; display:flex; align-items:center; gap:0.75rem; padding:0.875rem 1.125rem; background:hsl(var(--card)); border:1px solid hsl(var(--border)); border-radius:0.75rem; cursor:pointer; transition:all 0.15s ease; text-align:left; color:inherit; }
    .invite-btn:hover { border-color:hsl(var(--foreground)/0.25); background:hsl(var(--foreground)/0.04); transform:translateY(-1px); box-shadow:0 4px 16px rgba(0,0,0,0.12); }
    .invite-btn:active { transform:translateY(0); }
    .invite-btn-icon { display:flex; align-items:center; justify-content:center; width:2.25rem; height:2.25rem; border-radius:0.5rem; background:hsl(var(--muted)/0.5); color:hsl(var(--muted-foreground)); flex-shrink:0; }
    .invite-btn-text { display:flex; flex-direction:column; gap:0.125rem; }
    .invite-btn-label { font-size:0.875rem; font-weight:600; color:hsl(var(--foreground)); }
    .invite-btn-hint { font-size:0.6875rem; color:hsl(var(--muted-foreground)); }
    .panel-header { display:flex; align-items:center; gap:0.5rem; margin-bottom:1rem; }
    .panel-title { font-size:0.8125rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.08em; margin:0; }
    .room-count-badge { font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); background:hsl(var(--muted)/0.5); padding:0.125rem 0.5rem; border-radius:9999px; min-width:1.25rem; text-align:center; }
    @media (min-width:640px) {
      .hero { padding:0 2rem; }
      .main { padding:2rem; }
    }
    .resume-banner { display:flex; align-items:center; justify-content:center; gap:0.75rem; padding:0.625rem 1rem; background:hsl(var(--primary)/0.1); border-bottom:1px solid hsl(var(--primary)/0.25); cursor:pointer; transition:background 0.15s ease; }
    .resume-banner:hover { background:hsl(var(--primary)/0.15); }
    .resume-text { font-size:0.8125rem; font-weight:500; color:hsl(var(--primary)); }
    .resume-btn { padding:0.25rem 0.75rem; font-size:0.75rem; font-weight:600; background:hsl(var(--primary)); color:hsl(var(--primary-foreground)); border:none; border-radius:9999px; cursor:pointer; transition:opacity 0.15s ease; }
    .resume-btn:hover { opacity:0.85; }
    @media (max-width:480px) {
      .hero { padding:0 0.5rem; }
      .user-name { display:none; }
    }
    .footer { flex-shrink:0; padding:0.5rem 1rem; border-top:1px solid hsl(var(--border)); }
    .footer-inner { max-width:960px; margin:0 auto; display:flex; align-items:center; justify-content:center; gap:0.75rem; }
    .footer-links { display:flex; align-items:center; gap:0.625rem; }
    .footer-link { font-size:0.75rem; color:hsl(var(--muted-foreground)); text-decoration:none; transition:color 0.15s ease; }
    .footer-link:hover { color:hsl(var(--foreground)); }
    .footer-link-icon { display:inline-flex; align-items:center; gap:0.25rem; }
    .footer-dot { width:3px; height:3px; border-radius:50%; background:hsl(var(--muted-foreground)/0.4); }
    .best-player-link { display:inline-flex; align-items:center; gap:0.25rem; font-size:0.75rem; font-weight:600; color:hsl(var(--gold)); cursor:pointer; text-decoration:none; transition:opacity 0.15s ease; }
    .best-player-link:hover { opacity:0.8; }
    .footer-copy { font-size:0.6875rem; color:hsl(var(--muted-foreground)/0.6); margin-left:auto; }
  `],
})
export class HomeComponent implements OnInit, OnDestroy {
  readonly PlusIcon = Plus;
  readonly LogOutIcon = LogOut;
  readonly SettingsIcon = Settings;
  readonly TrophyIcon = Trophy;
  readonly GithubIcon = Github;
  readonly Share2Icon = Share2;
  readonly ZapIcon = Zap;
  readonly BotIcon = Bot;
  readonly currentYear = new Date().getFullYear();

  private readonly api = inject(ApiService);
  readonly session = inject(ClientSessionService);
  readonly auth = inject(AuthService);
  private readonly gameState = inject(GameStateService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(HotToastService);

  private pollSubscription: Subscription | null = null;
  private friendPollSubscription: Subscription | null = null;

  readonly rooms = signal<RoomResponse[]>([]);
  readonly loading = signal<boolean>(true);
  readonly showCreateForm = signal<boolean>(false);
  readonly showQuickGame = signal<boolean>(false);
  readonly aiTypes = signal<AiTypeInfo[]>([]);
  readonly pendingFriendCount = signal<number>(0);
  readonly activeGameRoomId = signal<string | null>(null);
  private readonly joining = signal(false);

  ngOnInit(): void {
    this.loadRooms();
    this.loadPendingFriendCount();
    this.loadAiTypes();
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

        // Detect active game from room list (works even without localStorage session)
        if (!this.activeGameRoomId()) {
          const disconnectedRoom = response.rooms.find(
            (r) => r.status === 'Playing' && r.isDisconnectedPlayer,
          );
          if (disconnectedRoom) {
            this.activeGameRoomId.set(disconnectedRoom.roomId);
          }
        }
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

  private loadAiTypes(): void {
    this.api.getAiTypes().subscribe({
      next: (types) => this.aiTypes.set(types),
    });
  }

  quickGame(event: { aiType: string; isRanked: boolean }): void {
    if (this.joining()) return;
    this.joining.set(true);
    this.showQuickGame.set(false);

    const aiSeats: AiSeat[] = [
      { position: PlayerPosition.Left, aiType: event.aiType },
      { position: PlayerPosition.Top, aiType: event.aiType },
      { position: PlayerPosition.Right, aiType: event.aiType },
    ];

    this.api.createRoom(null, aiSeats, 60, false, event.isRanked).subscribe({
      next: async (response) => {
        this.session.joinRoom(response.room.roomId, response.clientId, response.position);
        await this.gameState.enterRoom(response.room, true);
        this.router.navigate(['/table', response.room.roomId], { queryParams: { quickstart: 'true' } });
        this.joining.set(false);
      },
      error: () => {
        this.joining.set(false);
      },
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
    if (this.joining()) return;
    this.joining.set(true);
    this.api.joinRoom(event.room.roomId, event.position).subscribe({
      next: (response) => {
        if (response.position) {
          this.session.joinRoom(event.room.roomId, response.clientId, response.position);
          this.navigateToTable(response.room, false);
        }
        this.joining.set(false);
      },
      error: (err) => {
        console.error('Failed to join room', err);
        this.joining.set(false);
      },
    });
  }

  onRejoinRoom(room: RoomResponse): void {
    if (this.joining()) return;
    this.joining.set(true);
    this.api.rejoinRoom(room.roomId).subscribe({
      next: async (response) => {
        if (response.position) {
          this.session.joinRoom(room.roomId, response.clientId, response.position);
        }
        await this.gameState.enterRoom(response.room, response.room.isOwner);
        this.router.navigate(['/table', room.roomId]);
        this.joining.set(false);
      },
      error: (err) => {
        console.error('Failed to rejoin room', err);
        this.joining.set(false);
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
    if (!roomId || this.joining()) return;

    if (this.session.clientId()) {
      // Session intact — navigate directly
      this.router.navigate(['/table', roomId]);
    } else {
      // No clientId (new device / cleared storage) — rejoin via API first
      this.joining.set(true);
      this.api.rejoinRoom(roomId).subscribe({
        next: async (response) => {
          if (response.position) {
            this.session.joinRoom(roomId, response.clientId, response.position);
          }
          await this.gameState.enterRoom(response.room, response.room.isOwner);
          this.router.navigate(['/table', roomId]);
          this.joining.set(false);
        },
        error: (err) => {
          console.error('Failed to rejoin room', err);
          this.activeGameRoomId.set(null);
          this.joining.set(false);
        },
      });
    }
  }

  private checkActiveSession(): void {
    const roomId = this.session.roomId();
    if (!roomId) return;

    // Validate the session is still live (silent — room may have been cleaned up)
    this.api.tryGetRoom(roomId).subscribe((room) => {
      if (room?.status === 'Playing') {
        this.activeGameRoomId.set(roomId);
      } else {
        this.session.leaveRoom();
      }
    });
  }

  async inviteFriends(): Promise<void> {
    const url = window.location.origin;
    if (navigator.share) {
      try {
        await navigator.share({
          title: this.transloco.translate('home.shareTitle'),
          text: this.transloco.translate('home.shareText'),
          url,
        });
      } catch (err: unknown) {
        if (err instanceof DOMException && err.name === 'AbortError') return;
        await this.copyToClipboard(url);
      }
    } else {
      await this.copyToClipboard(url);
    }
  }

  private async copyToClipboard(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.toast.success(this.transloco.translate('home.linkCopied'));
    } catch {
      this.toast.error(this.transloco.translate('home.linkCopyFailed'));
    }
  }

  private async navigateToTable(room: RoomResponse, isCreator: boolean): Promise<void> {
    await this.gameState.enterRoom(room, isCreator);
    this.router.navigate(['/table', room.roomId]);
  }
}
