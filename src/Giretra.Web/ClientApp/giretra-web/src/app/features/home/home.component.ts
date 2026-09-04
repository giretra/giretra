import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService, RoomResponse, AiTypeInfo, AiSeat, AchievementShowcaseResponse } from '../../core/services/api.service';
import { JoinRoomEvent } from './components/room-list/room-list.component';
import { ClientSessionService } from '../../core/services/client-session.service';
import { AuthService } from '../../core/services/auth.service';
import { GameStateService } from '../../core/services/game-state.service';
import { GameHubService } from '../../api/game-hub.service';
import { RoomListComponent } from './components/room-list/room-list.component';
import { CreateRoomFormComponent } from './components/create-room-form/create-room-form.component';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { ErrorBannerService } from '../../core/services/error-banner.service';
import { QuickGameDialogComponent } from './components/quick-game-dialog/quick-game-dialog.component';
import { WelcomeDialogComponent } from '../../shared/components/welcome-dialog/welcome-dialog.component';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    RoomListComponent,
    CreateRoomFormComponent,
    TranslocoDirective,
    QuickGameDialogComponent,
    WelcomeDialogComponent,
    ButtonModule,
    TagModule,
    MessageModule,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="home">
      <!-- Resume game banner -->
      @if (activeGameRoomId()) {
        <p-message severity="success" icon="pi pi-play-circle" styleClass="resume-message">
          <div class="resume-row">
            <span class="resume-text">{{ t('home.resumeBanner') }}</span>
            <p-button [label]="t('home.resume')" icon="pi pi-arrow-right" iconPos="right" size="small" (onClick)="resumeGame()" />
          </div>
        </p-message>
      }

      <div class="grid grid-cols-12 gap-6">
        <!-- Quick game hero -->
        <div class="col-span-12 xl:col-span-8">
          <section class="g-card hero">
            <div class="hero-content">
              <span class="hero-badge"><i class="pi pi-bolt"></i></span>
              <h2 class="hero-title">{{ t('quickGame.title') }}</h2>
              <p class="hero-subtitle">{{ t('quickGame.subtitle') }}</p>
              <div class="hero-actions">
                <p-button
                  [label]="t('quickGame.play')"
                  icon="pi pi-play"
                  size="large"
                  styleClass="hero-btn"
                  (onClick)="showQuickGame.set(true)"
                />
                <span class="hero-meta"><i class="pi pi-clock"></i>{{ t('quickGame.turnTimer') }}</span>
              </div>
            </div>
            <div class="hero-art" aria-hidden="true">
              <span class="hero-card c1">♠</span>
              <span class="hero-card c2">♥</span>
              <span class="hero-card c3">♣</span>
            </div>
          </section>
        </div>

        <!-- Play with friends -->
        <div class="col-span-12 xl:col-span-4">
          <section class="g-card h-full">
            <div class="g-card-header">
              <div>
                <div class="g-card-title">{{ t('home.playWithFriends') }}</div>
                <p class="g-card-subtitle">{{ t('home.playWithFriendsHint') }}</p>
              </div>
            </div>
            <ul class="action-list">
              <li>
                <button type="button" class="action-row" (click)="showCreateForm.set(true)">
                  <span class="action-icon primary"><i class="pi pi-plus"></i></span>
                  <span class="action-text">
                    <span class="action-label">{{ t('home.createRoom') }}</span>
                    <span class="action-hint">{{ t('home.createRoomHint') }}</span>
                  </span>
                  <i class="pi pi-chevron-right action-chevron"></i>
                </button>
              </li>
              <li>
                <button type="button" class="action-row" (click)="inviteFriends()">
                  <span class="action-icon"><i class="pi pi-share-alt"></i></span>
                  <span class="action-text">
                    <span class="action-label">{{ t('home.inviteFriends') }}</span>
                    <span class="action-hint">{{ t('home.inviteFriendsHint') }}</span>
                  </span>
                  <i class="pi pi-chevron-right action-chevron"></i>
                </button>
              </li>
              @if (auth.user() && achievementShowcase(); as showcase) {
                <li>
                  <button type="button" class="action-row" (click)="goToAchievements()">
                    <span class="action-icon gold"><i class="pi pi-star"></i></span>
                    <span class="action-text">
                      <span class="action-label">{{ t('home.achievementsRow') }}</span>
                      <span class="action-hint">{{ t('home.achievementsHint', { earned: showcase.earnedCount, total: showcase.totalCount }) }}</span>
                    </span>
                    <i class="pi pi-chevron-right action-chevron"></i>
                  </button>
                </li>
              }
            </ul>
          </section>
        </div>

        <!-- Open tables -->
        <div class="col-span-12">
          <section class="g-card">
            <div class="g-card-header">
              <div>
                <div class="g-card-title">
                  {{ t('home.openTables') }}
                  <p-tag [value]="rooms().length.toString()" severity="secondary" [rounded]="true" />
                </div>
                <p class="g-card-subtitle">{{ t('home.openTablesHint') }}</p>
              </div>
              <p-button
                icon="pi pi-plus"
                severity="secondary"
                [text]="true"
                [rounded]="true"
                [attr.aria-label]="t('home.createRoom')"
                (onClick)="showCreateForm.set(true)"
              />
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
      </div>
    </div>

    <app-quick-game-dialog
      [open]="showQuickGame()"
      [aiTypes]="aiTypes()"
      (play)="quickGame($event)"
      (closed)="showQuickGame.set(false)"
      (createRoom)="showQuickGame.set(false); showCreateForm.set(true)"
    />

    <app-create-room-form
      [open]="showCreateForm()"
      (roomCreated)="onRoomCreated($event)"
      (cancelled)="showCreateForm.set(false)"
    />

    @if (showWelcome()) {
      <app-welcome-dialog (dismissed)="showWelcome.set(false)" />
    }
    </ng-container>
  `,
  styles: [`
    .home { display:flex; flex-direction:column; gap:1.5rem; }
    .resume-row { display:flex; align-items:center; justify-content:space-between; gap:1rem; flex:1; }
    .resume-text { font-weight:500; }

    .hero { position:relative; overflow:hidden; min-height:100%; display:flex; align-items:center; padding:2rem; border:none;
      background:linear-gradient(135deg, var(--p-primary-800) 0%, var(--p-primary-600) 55%, var(--p-primary-500) 100%); color:var(--p-primary-contrast-color); }
    .hero-content { position:relative; z-index:1; display:flex; flex-direction:column; align-items:flex-start; max-width:32rem; }
    .hero-badge { display:inline-flex; align-items:center; justify-content:center; width:2.75rem; height:2.75rem; border-radius:0.875rem; background:rgba(255,255,255,0.16); margin-bottom:1rem; }
    .hero-badge i { font-size:1.25rem; }
    .hero-title { margin:0 0 0.375rem; font-size:1.75rem; font-weight:700; line-height:1.15; }
    .hero-subtitle { margin:0 0 1.5rem; font-size:1rem; opacity:0.85; }
    .hero-actions { display:flex; align-items:center; flex-wrap:wrap; gap:1rem; }
    :host ::ng-deep .hero-btn { background:#fff; border-color:#fff; color:var(--p-primary-700); font-weight:600; }
    :host ::ng-deep .hero-btn:not(:disabled):hover { background:var(--p-primary-50); border-color:var(--p-primary-50); color:var(--p-primary-800); }
    .hero-meta { display:inline-flex; align-items:center; gap:0.375rem; font-size:0.8125rem; opacity:0.8; }
    .hero-art { position:absolute; right:1.5rem; top:50%; transform:translateY(-50%); display:flex; pointer-events:none; }
    .hero-card { display:flex; align-items:center; justify-content:center; width:4.5rem; height:6.25rem; border-radius:0.625rem; background:#fff; color:#1f2937; font-size:2rem; box-shadow:0 12px 30px rgba(0,0,0,0.25); }
    .hero-card.c1 { transform:rotate(-14deg) translate(1.75rem, 0.5rem); }
    .hero-card.c2 { color:#dc2626; transform:rotate(-2deg) translateY(-0.5rem); z-index:1; }
    .hero-card.c3 { transform:rotate(12deg) translate(-1.75rem, 0.5rem); }
    @media (max-width:639px) { .hero-art { display:none; } .hero { padding:1.5rem; } .hero-title { font-size:1.5rem; } }

    .action-list { list-style:none; margin:0; padding:0; display:flex; flex-direction:column; gap:0.25rem; }
    .action-row { width:100%; display:flex; align-items:center; gap:0.875rem; padding:0.625rem 0.5rem; border:none; border-radius:0.875rem; background:transparent; color:inherit; text-align:left; cursor:pointer; transition:background-color var(--transition-duration); }
    .action-row:hover { background:var(--surface-hover); }
    .action-icon { display:inline-flex; align-items:center; justify-content:center; width:2.5rem; height:2.5rem; border-radius:0.75rem; background:var(--p-surface-800); color:var(--text-color); flex-shrink:0; }
    .action-icon.primary { background:color-mix(in srgb, var(--p-primary-color) 22%, transparent); color:var(--p-primary-400); }
    .action-icon.gold { background:color-mix(in srgb, var(--p-yellow-400) 18%, transparent); color:var(--p-yellow-400); }
    .action-text { display:flex; flex-direction:column; gap:0.125rem; flex:1; min-width:0; }
    .action-label { font-weight:500; }
    .action-hint { font-size:0.8125rem; color:var(--text-color-secondary); }
    .action-chevron { color:var(--text-color-secondary); font-size:0.75rem; }
  `],
})
export class HomeComponent implements OnInit, OnDestroy {

  private readonly api = inject(ApiService);
  readonly session = inject(ClientSessionService);
  readonly auth = inject(AuthService);
  private readonly gameState = inject(GameStateService);
  private readonly hub = inject(GameHubService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly errorBanner = inject(ErrorBannerService);

  private roomsChangedSubscription: Subscription | null = null;
  private reconnectedSubscription: Subscription | null = null;

  readonly rooms = signal<RoomResponse[]>([]);
  readonly loading = signal<boolean>(true);
  readonly showCreateForm = signal<boolean>(false);
  readonly showQuickGame = signal<boolean>(false);
  readonly aiTypes = signal<AiTypeInfo[]>([]);
  readonly activeGameRoomId = signal<string | null>(null);
  readonly showWelcome = signal(WelcomeDialogComponent.shouldShow());
  private readonly joining = signal(false);
  readonly achievementShowcase = signal<AchievementShowcaseResponse | null>(null);

  ngOnInit(): void {
    this.loadRooms();
    this.loadAiTypes();
    this.loadAchievements();
    this.checkActiveSession();

    this.connectToLobby();
  }

  ngOnDestroy(): void {
    this.roomsChangedSubscription?.unsubscribe();
    this.reconnectedSubscription?.unsubscribe();
    this.hub.leaveLobby().catch(() => {});
  }

  private async connectToLobby(): Promise<void> {
    try {
      await this.hub.connect(environment.hubUrl);
      await this.hub.joinLobby();
    } catch (err) {
      console.error('[Home] Failed to connect to lobby', err);
    }

    this.roomsChangedSubscription = this.hub.roomsChanged$.subscribe(() => {
      this.loadRooms();
    });

    this.reconnectedSubscription = this.hub.reconnected$.subscribe(() => {
      this.hub.joinLobby().catch(() => {});
      this.loadRooms();
    });
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


  private loadAiTypes(): void {
    this.api.getAiTypes().subscribe({
      next: (types) => this.aiTypes.set(types),
    });
  }

  private loadAchievements(): void {
    if (!this.auth.user()) return;
    this.api.getMyAchievementShowcase().subscribe({
      next: (data) => this.achievementShowcase.set(data),
    });
  }

  goToAchievements(): void {
    this.router.navigate(['/achievements']);
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
      this.errorBanner.show(this.transloco.translate('home.linkCopied'));
    } catch {
      this.errorBanner.show(this.transloco.translate('home.linkCopyFailed'));
    }
  }

  private async navigateToTable(room: RoomResponse, isCreator: boolean): Promise<void> {
    await this.gameState.enterRoom(room, isCreator);
    this.router.navigate(['/table', room.roomId]);
  }
}
