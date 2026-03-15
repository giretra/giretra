import { Component, input, output, computed, inject, signal, HostListener } from '@angular/core';
import { GameMode, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse } from '../../../../core/services/api.service';
import { getTeamLabel } from '../../../../core/utils';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { FullscreenService } from '../../../../core/services/fullscreen.service';
import { SoundService } from '../../../../core/services/sound.service';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { TurnTimerComponent } from '../../../../shared/components/turn-timer/turn-timer.component';
import { HlmButton } from '@spartan-ng/helm/button';
import {
  LucideAngularModule,
  Menu,
  LogOut,
  Maximize,
  Minimize,
  Volume2,
  VolumeOff,
  Copy,
  Globe,
  MessageCircle,
} from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { LanguageSwitcherComponent } from '../../../../shared/components/language-switcher/language-switcher.component';
import { HotToastService } from '@ngxpert/hot-toast';

@Component({
  selector: 'app-score-bar',
  standalone: true,
  imports: [
    GameModeBadgeComponent,
    MultiplierBadgeComponent,
    TurnTimerComponent,
    HlmButton,
    LucideAngularModule,
    TranslocoDirective,
    LanguageSwitcherComponent,
  ],
  template: `
    <div class="score-bar" *transloco="let t">
      <div class="bar-content">
        <!-- Left group: menu + scores + mode -->
        <div class="left-group">
          <!-- Burger menu -->
          <button class="icon-btn" (click)="toggleMenu($event)" [title]="t('scoreBar.menu')">
            <i-lucide [img]="MenuIcon" [size]="18" [strokeWidth]="1.5"></i-lucide>
          </button>

          <div class="separator"></div>

          <!-- Scores -->
          <button class="scores" (click)="matchPointsClicked.emit()">
            <span class="score-group">
              <span class="score-label">{{ team1Label() }}</span>
              <span class="score-value team1-color">{{ team1MatchPoints() }}</span>
            </span>
            <span class="score-dash">:</span>
            <span class="score-group">
              <span class="score-value team2-color">{{ team2MatchPoints() }}</span>
              <span class="score-label">{{ team2Label() }}</span>
            </span>
          </button>

          <!-- Game mode (always visible when set) -->
          @if (gameMode()) {
            <div class="separator"></div>
            <button class="mode-chip" (click)="modeBadgeClicked.emit()">
              <app-game-mode-badge [mode]="gameMode()" class="mode-full" />
              <app-game-mode-badge [mode]="gameMode()" [compact]="true" class="mode-compact" />
              <app-multiplier-badge [multiplier]="multiplier()" class="multiplier-badge" />
            </button>
          }

          <!-- Chat -->
          <div class="separator"></div>
          <button class="icon-btn chat-btn" (click)="chatClicked.emit()" [title]="t('chat.title')">
            <i-lucide [img]="MessageCircleIcon" [size]="17" [strokeWidth]="1.5"></i-lucide>
            @if (unreadCount() > 0) {
              <span class="chat-badge">{{ unreadCount() > 9 ? '9+' : unreadCount() }}</span>
            }
          </button>
        </div>

        <!-- Right group: status info -->
        <div class="right-group">
          @if (isMyTurn() && turnTimeoutAt()) {
            <div class="turn-badge">
              <span class="turn-dot"></span>
              <span class="turn-text">{{ t('scoreBar.yourTurn') }}</span>
              <app-turn-timer [deadline]="turnTimeoutAt()" />
            </div>
          } @else if (dealNumber() > 0) {
            <span class="info-text">{{ t('scoreBar.dealNumber', { number: dealNumber() }) }}</span>
          } @else {
            <span class="info-text">{{ room()?.name }}</span>
          }
        </div>
      </div>

      <!-- Deal progress -->
      @if (showDealPoints()) {
        <div class="progress-row">
          <div class="progress-track">
            <div class="progress-fill team1-fill" [style.width.%]="team1ProgressPercent()"></div>
          </div>
          <span class="progress-pts team1-color">{{ team1CardPoints() }}</span>
          <span class="progress-sep">|</span>
          <span class="progress-pts team2-color">{{ team2CardPoints() }}</span>
          <div class="progress-track">
            <div class="progress-fill team2-fill" [style.width.%]="team2ProgressPercent()"></div>
          </div>
        </div>
      }

      <!-- Menu dropdown -->
      @if (menuOpen()) {
        <div class="menu-backdrop" (click)="menuOpen.set(false)"></div>
        <div class="menu-panel">
          @if (room()?.roomId) {
            <button class="menu-row" (click)="copyRoomCode()">
              <i-lucide [img]="CopyIcon" [size]="15" [strokeWidth]="1.5"></i-lucide>
              <span class="menu-label">{{ t('scoreBar.roomCode') }}</span>
              <span class="menu-secondary">{{ room()?.name || room()?.roomId }}</span>
            </button>
          }

          <button class="menu-row" (click)="onToggleSound()">
            <i-lucide [img]="sound.muted() ? VolumeOffIcon : Volume2Icon" [size]="15" [strokeWidth]="1.5"></i-lucide>
            <span class="menu-label">{{ t('scoreBar.sound') }}</span>
            <span class="menu-toggle" [class.active]="!sound.muted()">
              <span class="menu-toggle-thumb"></span>
            </span>
          </button>

          @if (fullscreen.isFullscreenSupported) {
            <button class="menu-row" (click)="onToggleFullscreen()">
              <i-lucide [img]="fullscreen.isFullscreen() ? MinimizeIcon : MaximizeIcon" [size]="15" [strokeWidth]="1.5"></i-lucide>
              <span class="menu-label">{{ fullscreen.isFullscreen() ? t('fullscreen.exit') : t('fullscreen.enter') }}</span>
            </button>
          }

          <div class="menu-row menu-row-lang">
            <i-lucide [img]="GlobeIcon" [size]="15" [strokeWidth]="1.5"></i-lucide>
            <span class="menu-label">{{ t('scoreBar.language') }}</span>
            <app-language-switcher />
          </div>

          <div class="menu-sep"></div>

          <button class="menu-row menu-row-danger" (click)="onLeaveFromMenu()">
            <i-lucide [img]="LogOutIcon" [size]="15" [strokeWidth]="1.5"></i-lucide>
            <span class="menu-label">{{ t('scoreBar.leaveTable') }}</span>
          </button>
        </div>
      }
    </div>
  `,
  styles: [
    `
      /* ── Bar: frosted glass ── */
      .score-bar {
        background: hsl(var(--card) / 0.72);
        backdrop-filter: blur(20px) saturate(1.6);
        -webkit-backdrop-filter: blur(20px) saturate(1.6);
        border-bottom: 0.5px solid hsl(var(--foreground) / 0.08);
        padding: 0 0.75rem;
        flex-shrink: 0;
        position: relative;
        z-index: 10;
      }

      .bar-content {
        display: flex;
        align-items: center;
        justify-content: space-between;
        min-height: 2.75rem;
        gap: 0.75rem;
      }

      /* ── Left group ── */
      .left-group {
        display: flex;
        align-items: center;
        gap: 0.625rem;
        flex-shrink: 0;
      }

      .separator {
        width: 0.5px;
        height: 1.125rem;
        background: hsl(var(--foreground) / 0.12);
        flex-shrink: 0;
      }

      /* ── Icon button (burger) ── */
      .icon-btn {
        display: grid;
        place-items: center;
        width: 2rem;
        height: 2rem;
        border: none;
        border-radius: 0.5rem;
        background: transparent;
        color: hsl(var(--foreground) / 0.72);
        cursor: pointer;
        transition: background 0.15s ease, color 0.15s ease;
        flex-shrink: 0;
      }

      .icon-btn:hover {
        background: hsl(var(--foreground) / 0.06);
        color: hsl(var(--foreground));
      }

      .icon-btn:active {
        background: hsl(var(--foreground) / 0.1);
      }

      /* ── Chat button with badge ── */
      .chat-btn {
        position: relative;
      }

      .chat-badge {
        position: absolute;
        top: 0;
        right: 0;
        min-width: 0.875rem;
        height: 0.875rem;
        padding: 0 0.25rem;
        border-radius: 9999px;
        background: hsl(0 72% 51%);
        color: #fff;
        font-size: 0.5625rem;
        font-weight: 700;
        display: flex;
        align-items: center;
        justify-content: center;
        line-height: 1;
        pointer-events: none;
        animation: badgeIn 0.2s ease;
      }

      @keyframes badgeIn {
        from { transform: scale(0); }
        to { transform: scale(1); }
      }

      /* ── Scores: pure typography ── */
      .scores {
        display: flex;
        align-items: baseline;
        gap: 0.25rem;
        border: none;
        background: transparent;
        cursor: pointer;
        padding: 0.25rem 0.125rem;
        border-radius: 0.375rem;
        transition: background 0.15s ease;
        font: inherit;
        color: inherit;
      }

      .scores:hover {
        background: hsl(var(--foreground) / 0.05);
      }

      .scores:active {
        background: hsl(var(--foreground) / 0.08);
      }

      .score-group {
        display: flex;
        align-items: baseline;
        gap: 0.3125rem;
      }

      .score-label {
        font-size: 0.6875rem;
        font-weight: 500;
        color: hsl(var(--foreground) / 0.4);
        text-transform: uppercase;
        letter-spacing: 0.02em;
      }

      .score-value {
        font-size: 1.25rem;
        font-weight: 600;
        line-height: 1;
        font-variant-numeric: tabular-nums;
        letter-spacing: -0.02em;
      }

      .team1-color {
        color: hsl(var(--team1));
      }

      .team2-color {
        color: hsl(var(--team2));
      }

      .score-dash {
        font-size: 0.875rem;
        font-weight: 400;
        color: hsl(var(--foreground) / 0.2);
        margin: 0 0.0625rem;
      }

      /* ── Mode chip: flat, no border ── */
      .mode-chip {
        display: flex;
        align-items: center;
        gap: 0.375rem;
        padding: 0.25rem 0.5rem;
        border: none;
        border-radius: 0.375rem;
        background: hsl(var(--gold) / 0.06);
        cursor: pointer;
        font: inherit;
        color: inherit;
        transition: background 0.15s ease;
      }

      .mode-chip:hover {
        background: hsl(var(--gold) / 0.12);
      }

      .mode-chip:active {
        background: hsl(var(--gold) / 0.16);
      }

      /* Full badge visible on desktop, compact on mobile */
      .mode-compact {
        display: none;
      }

      .mode-full {
        display: inline-flex;
      }

      /* ── Right group ── */
      .right-group {
        display: flex;
        align-items: center;
        flex: 1;
        justify-content: flex-end;
        min-width: 0;
      }

      .info-text {
        font-size: 0.8125rem;
        font-weight: 400;
        color: hsl(var(--foreground) / 0.36);
      }

      /* ── Turn badge ── */
      .turn-badge {
        display: flex;
        align-items: center;
        gap: 0.4375rem;
        padding: 0.25rem 0.625rem;
        border-radius: 9999px;
        background: hsl(var(--primary) / 0.1);
        animation: turnPulseIn 0.3s ease;
      }

      .turn-dot {
        width: 0.375rem;
        height: 0.375rem;
        border-radius: 50%;
        background: hsl(var(--primary));
        animation: dotPulse 1.5s ease-in-out infinite;
      }

      .turn-text {
        font-size: 0.6875rem;
        font-weight: 600;
        color: hsl(var(--primary));
        text-transform: uppercase;
        letter-spacing: 0.03em;
      }

      @keyframes turnPulseIn {
        from {
          opacity: 0;
          transform: scale(0.92);
        }
        to {
          opacity: 1;
          transform: scale(1);
        }
      }

      @keyframes dotPulse {
        0%,
        100% {
          opacity: 1;
        }
        50% {
          opacity: 0.4;
        }
      }

      /* ── Progress row ── */
      .progress-row {
        display: flex;
        align-items: center;
        gap: 0.375rem;
        padding: 0 0.125rem 0.375rem;
      }

      .progress-track {
        flex: 1;
        height: 3px;
        background: hsl(var(--foreground) / 0.06);
        border-radius: 1.5px;
        overflow: hidden;
      }

      .progress-fill {
        height: 100%;
        border-radius: 1.5px;
        transition: width 0.5s cubic-bezier(0.4, 0, 0.2, 1);
      }

      .team1-fill {
        background: hsl(var(--team1) / 0.7);
      }

      .team2-fill {
        background: hsl(var(--team2) / 0.7);
      }

      .progress-pts {
        font-size: 0.625rem;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
        min-width: 1.25rem;
        text-align: center;
      }

      .progress-sep {
        font-size: 0.5rem;
        color: hsl(var(--foreground) / 0.12);
      }

      /* ── Menu backdrop ── */
      .menu-backdrop {
        position: fixed;
        inset: 0;
        z-index: 99;
      }

      /* ── Menu panel: frosted glass ── */
      .menu-panel {
        position: absolute;
        top: calc(100% + 6px);
        left: 0.75rem;
        z-index: 100;
        min-width: 230px;
        background: hsl(var(--card) / 0.82);
        backdrop-filter: blur(40px) saturate(1.8);
        -webkit-backdrop-filter: blur(40px) saturate(1.8);
        border: 0.5px solid hsl(var(--foreground) / 0.1);
        border-radius: 0.75rem;
        padding: 0.25rem;
        box-shadow:
          0 0 0 0.5px hsl(0 0% 0% / 0.12),
          0 8px 40px hsl(0 0% 0% / 0.45);
        animation: menuIn 0.18s cubic-bezier(0.2, 0, 0, 1);
      }

      @keyframes menuIn {
        from {
          opacity: 0;
          transform: translateY(-6px) scale(0.97);
        }
        to {
          opacity: 1;
          transform: translateY(0) scale(1);
        }
      }

      .menu-row {
        display: flex;
        align-items: center;
        gap: 0.625rem;
        width: 100%;
        padding: 0.5rem 0.75rem;
        border: none;
        border-radius: 0.5rem;
        background: transparent;
        color: hsl(var(--foreground));
        font: inherit;
        font-size: 0.8125rem;
        font-weight: 400;
        cursor: pointer;
        transition: background 0.1s ease;
        text-align: left;
      }

      .menu-row:hover {
        background: hsl(var(--foreground) / 0.06);
      }

      .menu-row:active {
        background: hsl(var(--foreground) / 0.1);
      }

      .menu-label {
        flex: 1;
      }

      .menu-secondary {
        font-size: 0.75rem;
        color: hsl(var(--foreground) / 0.36);
        font-weight: 400;
      }

      .menu-row-danger {
        color: hsl(0 72% 65%);
      }

      .menu-row-danger:hover {
        background: hsl(0 72% 51% / 0.08);
      }

      .menu-row-lang {
        cursor: default;
      }

      .menu-row-lang:hover {
        background: transparent;
      }

      .menu-sep {
        height: 0.5px;
        background: hsl(var(--foreground) / 0.08);
        margin: 0.25rem 0.5rem;
      }

      /* ── Toggle switch (Apple-style) ── */
      .menu-toggle {
        position: relative;
        width: 2rem;
        height: 1.1875rem;
        border-radius: 9999px;
        background: hsl(var(--foreground) / 0.12);
        transition: background 0.25s ease;
        flex-shrink: 0;
      }

      .menu-toggle.active {
        background: hsl(var(--primary));
      }

      .menu-toggle-thumb {
        position: absolute;
        top: 0.125rem;
        left: 0.125rem;
        width: 0.9375rem;
        height: 0.9375rem;
        border-radius: 50%;
        background: hsl(var(--foreground));
        box-shadow: 0 1px 3px hsl(0 0% 0% / 0.2);
        transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
      }

      .menu-toggle.active .menu-toggle-thumb {
        transform: translateX(0.8125rem);
      }

      /* ── Mobile ── */
      @media (max-width: 480px) {
        .score-bar {
          padding: 0 0.5rem;
        }

        .bar-content {
          gap: 0.5rem;
          min-height: 2.5rem;
        }

        .left-group {
          gap: 0.375rem;
        }

        .score-value {
          font-size: 1.0625rem;
        }

        .score-label {
          font-size: 0.5625rem;
        }

        /* Show icon-only mode badge */
        .mode-full {
          display: none;
        }

        .mode-compact {
          display: inline-flex;
        }

        /* Hide multiplier text on mobile */
        .multiplier-badge {
          display: none;
        }

        .mode-chip {
          padding: 0.1875rem 0.3125rem;
          gap: 0.125rem;
        }

        /* Compact turn indicator: hide text, show dot + timer */
        .turn-text {
          display: none;
        }

        .turn-badge {
          padding: 0.1875rem 0.5rem;
          gap: 0.3125rem;
        }

        .info-text {
          font-size: 0.75rem;
        }
      }

      @media (max-width: 360px) {
        .score-label {
          display: none;
        }

        .separator {
          display: none;
        }
      }
    `,
  ],
})
export class ScoreBarComponent {
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(HotToastService);
  readonly fullscreen = inject(FullscreenService);
  readonly sound = inject(SoundService);

  readonly MenuIcon = Menu;
  readonly LogOutIcon = LogOut;
  readonly MaximizeIcon = Maximize;
  readonly MinimizeIcon = Minimize;
  readonly Volume2Icon = Volume2;
  readonly VolumeOffIcon = VolumeOff;
  readonly CopyIcon = Copy;
  readonly GlobeIcon = Globe;
  readonly MessageCircleIcon = MessageCircle;

  readonly room = input<RoomResponse | null>(null);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly team1CardPoints = input<number>(0);
  readonly team2CardPoints = input<number>(0);
  readonly dealNumber = input<number>(0);
  readonly gameMode = input<GameMode | null>(null);
  readonly multiplier = input<MultiplierState>('Normal');
  readonly myTeam = input<Team | null>(null);
  readonly isMyTurn = input<boolean>(false);
  readonly turnTimeoutAt = input<Date | null>(null);

  readonly unreadCount = input<number>(0);

  readonly leaveTable = output<void>();
  readonly modeBadgeClicked = output<void>();
  readonly matchPointsClicked = output<void>();
  readonly chatClicked = output<void>();

  readonly menuOpen = signal(false);

  readonly showDealPoints = computed(() => this.gameMode() !== null);

  readonly team1Label = computed(() =>
    getTeamLabel('Team1', this.myTeam(), (k) => this.transloco.translate(k)),
  );
  readonly team2Label = computed(() =>
    getTeamLabel('Team2', this.myTeam(), (k) => this.transloco.translate(k)),
  );

  readonly totalPoints = computed(() => {
    const mode = this.gameMode();
    if (!mode) return 162;
    if (mode === GameMode.AllTrumps) return 258;
    if (mode === GameMode.NoTrumps) return 130;
    return 162;
  });

  readonly thresholdPercent = computed(() => {
    const mode = this.gameMode();
    if (!mode) return 50;
    if (mode === GameMode.AllTrumps) return (129 / 258) * 100;
    if (mode === GameMode.NoTrumps) return (65 / 130) * 100;
    return (82 / 162) * 100;
  });

  readonly team1ProgressPercent = computed(() => {
    const total = this.totalPoints();
    return Math.min((this.team1CardPoints() / total) * 100, 100);
  });

  readonly team2ProgressPercent = computed(() => {
    const total = this.totalPoints();
    return Math.min((this.team2CardPoints() / total) * 100, 100);
  });

  toggleMenu(event: Event): void {
    event.stopPropagation();
    this.menuOpen.update((v) => !v);
  }

  onToggleSound(): void {
    this.sound.toggleMute();
  }

  onToggleFullscreen(): void {
    this.fullscreen.toggle();
    this.menuOpen.set(false);
  }

  onLeaveFromMenu(): void {
    this.menuOpen.set(false);
    this.leaveTable.emit();
  }

  async copyRoomCode(): Promise<void> {
    const roomId = this.room()?.roomId;
    if (!roomId) return;
    try {
      await navigator.clipboard.writeText(roomId);
      this.toast.success(this.transloco.translate('scoreBar.copied'));
    } catch {
      this.toast.error(this.transloco.translate('scoreBar.copyFailed'));
    }
    this.menuOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.menuOpen()) {
      this.menuOpen.set(false);
    }
  }
}
