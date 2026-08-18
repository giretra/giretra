import { Component, inject, OnInit, OnDestroy, effect, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, ActivatedRoute } from '@angular/router';
import { GameStateService, GamePhase, MultiplierState } from '../../core/services/game-state.service';
import { ClientSessionService } from '../../core/services/client-session.service';
import { ApiService, PlayerProfileResponse } from '../../core/services/api.service';
import { GameHubService } from '../../api/game-hub.service';
import { FullscreenService } from '../../core/services/fullscreen.service';
import { AchievementEarnedDto, GameMode, PendingActionType, PlayerPosition, SeatAccessMode } from '../../api/generated/signalr-types.generated';
import { getTeam } from '../../core/utils/position-utils';
import { ScoreBarComponent } from './components/score-bar/score-bar.component';
import { TableSurfaceComponent } from './components/table-surface/table-surface.component';
import { HandAreaComponent } from './components/hand-area/hand-area.component';
import { MatchEndOverlayComponent } from './components/center-stage/match-end-overlay/match-end-overlay.component';
import { BidDialogComponent } from './components/bid-dialog/bid-dialog.component';
import { GameModePopupComponent } from './components/game-mode-popup/game-mode-popup.component';
import { NegotiationHistoryPopupComponent } from './components/negotiation-history-popup/negotiation-history-popup.component';
import { WonTricksPopupComponent } from './components/won-tricks-popup/won-tricks-popup.component';
import { CutInfoPopupComponent } from './components/cut-info-popup/cut-info-popup.component';
import { CutOutcome } from './components/center-stage/cut-stage/cut-deck-3d.component';
import { MatchHistoryPopupComponent } from './components/match-history-popup/match-history-popup.component';
import { PlayerProfilePopupComponent } from '../../shared/components/player-profile-popup/player-profile-popup.component';
import { ChatPopupComponent } from './components/chat-popup/chat-popup.component';
import { ChatService } from '../../core/services/chat.service';
import { environment } from '../../../environments/environment';
import { SoundService } from '../../core/services/sound.service';
import { LucideAngularModule, Maximize, MessageCircle, Award, X } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { ErrorBannerService } from '../../core/services/error-banner.service';

@Component({
  selector: 'app-table',
  standalone: true,
  imports: [
    ScoreBarComponent,
    TableSurfaceComponent,
    HandAreaComponent,
    MatchEndOverlayComponent,
    BidDialogComponent,
    GameModePopupComponent,
    NegotiationHistoryPopupComponent,
    WonTricksPopupComponent,
    CutInfoPopupComponent,
    MatchHistoryPopupComponent,
    PlayerProfilePopupComponent,
    ChatPopupComponent,
    LucideAngularModule,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="table-container"
         [class.bid-dialog-open]="gameState.phase() === 'negotiation' && gameState.isMyTurn() && gameState.pendingActionType() === 'Negotiate'"
         [class.deal-summary-open]="gameState.dealSummary()">

      <!-- Connection Status Banner -->
      @if (hub.connectionStatus() === 'reconnecting') {
        <div class="connection-banner reconnecting">
          <span class="connection-spinner"></span>
          {{ t('table.reconnecting') }}
        </div>
      } @else if (hub.connectionStatus() === 'disconnected' && gameState.gameId()) {
        <div class="connection-banner disconnected">
          {{ t('table.connectionLost') }}
          <button class="retry-btn" (click)="onRetryConnection()">{{ t('common.retry') }}</button>
        </div>
      }

      <!-- Error Banner -->
      @if (errorBanner.message()) {
        <div class="connection-banner error">
          {{ errorBanner.message() }}
        </div>
      }

      <!-- Fullscreen suggestion (mobile only, one-time) -->
      @if (showFullscreenSuggestion()) {
        <div class="fullscreen-banner">
          <span class="fullscreen-banner-text">{{ t('fullscreen.suggestion') }}</span>
          <button class="fullscreen-banner-btn" (click)="acceptFullscreen()">
            <i-lucide [img]="MaximizeIcon" [size]="14" [strokeWidth]="2"></i-lucide>
            {{ t('fullscreen.goFullscreen') }}
          </button>
          <button class="fullscreen-banner-dismiss" (click)="dismissFullscreen()">{{ t('fullscreen.notNow') }}</button>
        </div>
      }

      <!-- Zone A: Score Bar -->
      <app-score-bar
        [room]="gameState.currentRoom()"
        [team1MatchPoints]="gameState.team1MatchPoints()"
        [team2MatchPoints]="gameState.team2MatchPoints()"
        [dealNumber]="gameState.dealNumber()"
        [gameMode]="gameState.gameMode()"
        [multiplier]="gameState.multiplier()"
        [myTeam]="gameState.myTeam()"
        [isMyTurn]="gameState.isMyTurn()"
        [turnTimeoutAt]="gameState.turnTimeoutAt()"
        [unreadCount]="chatService.unreadCount()"
        [gameInProgress]="gameInProgress()"
        (leaveTable)="onLeaveTable()"
        (modeBadgeClicked)="onModeBadgeClicked()"
        (matchPointsClicked)="onMatchPointsClicked()"
        (chatClicked)="onChatClicked()"
      />

      <!-- Zone B: Table Surface (players + center stage) -->
      <app-table-surface
        [room]="gameState.currentRoom()"
        [phase]="gameState.phase()"
        [myPosition]="gameState.myPosition()"
        [activePlayer]="gameState.activePlayer()"
        [currentTrick]="gameState.currentTrick()"
        [completedTrickToShow]="gameState.completedTrickToShow()"
        [showingCompletedTrick]="gameState.showingCompletedTrick()"
        [gameMode]="gameState.gameMode()"
        [dealer]="gameState.dealer()"
        [negotiationHistory]="gameState.negotiationHistory()"
        [isCreator]="gameState.isCreator()"
        [isWatcher]="gameState.isWatcher()"
        [dealSummary]="gameState.dealSummary()"
        [matchWinner]="gameState.matchWinner()"
        [team1MatchPoints]="gameState.team1MatchPoints()"
        [team2MatchPoints]="gameState.team2MatchPoints()"
        [completedDeals]="gameState.dealNumber() - 1"
        [playerCardCounts]="gameState.playerCardCounts()"
        [team1Tricks]="gameState.team1TricksWon()"
        [team2Tricks]="gameState.team2TricksWon()"
        [myTeam]="gameState.myTeam()"
        [tricksWonByPosition]="gameState.tricksWonByPosition()"
        [idleDeadline]="gameState.idleDeadline()"
        [waitingForContinue]="waitingForContinue()"
        [cutOutcome]="cutOutcome()"
        (startGame)="onStartGame()"
        (submitCut)="onSubmitCut($event)"
        (cutAnimationDone)="onCutAnimationDone()"
        (cutInfo)="showCutInfo.set(true)"
        (hideDealSummary)="onHideDealSummary()"
        (dismissCompletedTrick)="onDismissCompletedTrick()"
        (setSeatMode)="onSetSeatMode($event)"
        (generateInvite)="onGenerateInvite($event)"
        (kickPlayer)="onKickPlayer($event)"
        (seatClicked)="onSeatClicked($event)"
        (tricksClicked)="onTricksClicked($event)"
      />

      <!-- Zone C: Hand / Action Area -->
      <app-hand-area
        [phase]="gameState.phase()"
        [isMyTurn]="gameState.isMyTurn()"
        [isWatcher]="gameState.isWatcher()"
        [hand]="gameState.hand()"
        [validCards]="gameState.validCards()"
        [gameMode]="gameState.gameMode()"
        [activePlayer]="gameState.activePlayer()"
        [playerCardCounts]="gameState.playerCardCounts()"
        [disabled]="gameState.isSubmittingAction()"
        [team1MatchPoints]="gameState.team1MatchPoints()"
        [team2MatchPoints]="gameState.team2MatchPoints()"
        [multiplier]="gameState.multiplier()"
        [isRanked]="gameState.isRanked()"
        [dealNumber]="gameState.dealNumber()"
        [idleDeadline]="gameState.idleDeadline()"
        (playCard)="onPlayCard($event)"
        (idleExpired)="onLeaveTable()"
      />

      <!-- Bid Dialog Overlay -->
      @if (gameState.phase() === 'negotiation' && gameState.isMyTurn() && gameState.pendingActionType() === 'Negotiate') {
        <app-bid-dialog
          [validActions]="gameState.validActions()"
          [negotiationHistory]="gameState.negotiationHistory()"
          [activePlayer]="gameState.activePlayer()"
          (actionSelected)="onSubmitNegotiation($event)"
        />
      }

      <!-- Game Mode Popup (shown briefly after negotiation ends) -->
      @if (gameModePopup(); as popup) {
        <app-game-mode-popup
          [gameMode]="popup.mode"
          [multiplier]="popup.multiplier"
          (dismissed)="dismissGameModePopup()"
        />
      }

      <!-- Cut Info Popup -->
      @if (showCutInfo()) {
        <app-cut-info-popup (closed)="showCutInfo.set(false)" />
      }

      <!-- Won Tricks Popup -->
      @if (showWonTricks()) {
        <app-won-tricks-popup
          [tricks]="gameState.completedTricks()"
          [myTeam]="gameState.myTeam()"
          [myPosition]="gameState.myPosition()"
          (closed)="showWonTricks.set(false)"
        />
      }

      <!-- Negotiation History Popup -->
      @if (showNegotiationHistory()) {
        <app-negotiation-history-popup
          [negotiationHistory]="gameState.negotiationHistory()"
          [multiplier]="gameState.multiplier()"
          (closed)="showNegotiationHistory.set(false)"
        />
      }

      <!-- Match History Popup -->
      @if (showMatchHistory()) {
        <app-match-history-popup
          [dealHistory]="gameState.matchDealHistory()"
          [team1MatchPoints]="gameState.team1MatchPoints()"
          [team2MatchPoints]="gameState.team2MatchPoints()"
          [myTeam]="gameState.myTeam()"
          (closed)="showMatchHistory.set(false)"
        />
      }

      <!-- Match End Overlay -->
      @if (gameState.phase() === 'matchEnd') {
        <app-match-end-overlay
          [winner]="gameState.matchWinner()"
          [myTeam]="gameState.myTeam()"
          [myPosition]="gameState.myPosition()"
          [team1Points]="gameState.team1MatchPoints()"
          [team2Points]="gameState.team2MatchPoints()"
          [totalDeals]="gameState.dealNumber() - 1"
          [completedDeals]="gameState.completedDeals()"
          [eloChange]="gameState.myEloChange()"
          [isRanked]="gameState.isRanked()"
          [isWatcher]="gameState.isWatcher()"
          [idleDeadline]="gameState.idleDeadline()"
          [playAgainDeadline]="gameState.turnTimeoutAt()"
          [waiting]="waitingForContinue()"
          [achievements]="gameState.earnedAchievements()"
          (playAgain)="onPlayAgain()"
          (leaveTable)="onLeaveTable()"
        />
      }

      <!-- Achievements FAB -->
      @if (myAchievements().length > 0 && !showAchievementsPopup()) {
        <button class="achievements-fab" (click)="showAchievementsPopup.set(true)" [title]="t('achievements.title')">
          <i-lucide [img]="AwardIcon" [size]="18" [strokeWidth]="1.5"></i-lucide>
          <span class="achievements-fab-badge">{{ myAchievements().length }}</span>
        </button>
      }

      <!-- Achievements Popup -->
      @if (showAchievementsPopup() && myAchievements().length > 0) {
        <div class="achievements-popup-backdrop" (click)="showAchievementsPopup.set(false)">
          <div class="achievements-popup" (click)="$event.stopPropagation()">
            <div class="achievements-popup-header">
              <i-lucide [img]="AwardIcon" [size]="16" [strokeWidth]="2"></i-lucide>
              <span>{{ myAchievements().length === 1 ? t('achievements.earned') : t('achievements.earnedMultiple', { count: myAchievements().length }) }}</span>
              <button class="achievements-popup-close" (click)="showAchievementsPopup.set(false)">
                <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
              </button>
            </div>
            <div class="achievements-popup-list">
              @for (ach of myAchievements(); track ach.code) {
                <div
                  class="ach-item"
                  [class.tier-high]="ach.tier >= 4"
                  [class.tier-mid]="ach.tier === 3"
                  [class.hidden-ach]="ach.isHidden"
                  [class.expanded]="expandedAchievement() === ach.code"
                  [style.animation-delay]="$index * 100 + 'ms'"
                  (click)="toggleAchievementExpand(ach.code)"
                >
                  <div class="ach-info">
                    @if (ach.isHidden) {
                      <span class="ach-name ach-hidden-text">???</span>
                    } @else {
                      <span class="ach-name">{{ ach.name }}</span>
                    }
                    <span class="ach-stars">
                      @for (_ of tierDots(ach.tier); track $index) {
                        <span class="ach-star filled" [style.animation-delay]="($index * 80) + 'ms'">★</span>
                      }
                      @for (_ of tierDots(5 - ach.tier); track $index) {
                        <span class="ach-star empty">★</span>
                      }
                    </span>
                    @if (expandedAchievement() === ach.code && !ach.isHidden) {
                      <span class="ach-desc">{{ t('achievements.desc.' + ach.code) }}</span>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- Chat FAB -->
      @if (!chatService.isPopupOpen()) {
        <button class="chat-fab" (click)="onChatClicked()" [title]="t('chat.title')">
          <i-lucide [img]="MessageCircleIcon" [size]="22" [strokeWidth]="1.5"></i-lucide>
          @if (chatService.unreadCount() > 0) {
            <span class="chat-fab-badge">{{ chatService.unreadCount() > 9 ? '9+' : chatService.unreadCount() }}</span>
          }
        </button>
      }

      <!-- Chat Popup -->
      @if (chatService.isPopupOpen()) {
        <app-chat-popup
          [messages]="chatService.messages()"
          [isChatEnabled]="chatService.isChatEnabled()"
          (messageSent)="onChatMessageSent($event)"
          (closed)="chatService.closePopup()"
        />
      }

      <!-- Player Profile Popup -->
      @if (profilePopupData()) {
        <app-player-profile-popup
          [profile]="profilePopupData()!"
          [teamClass]="profilePopupTeam()"
          (closed)="closeProfilePopup()"
        />
      }
    </div>
    </ng-container>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
    }

    .table-container {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 100vh;
      min-height: 100dvh;
      background: hsl(var(--background));
      overflow: hidden;
    }

    app-score-bar {
      flex-shrink: 0;
    }

    app-table-surface {
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    app-hand-area {
      flex-shrink: 0;
    }

    .deal-summary-open app-table-surface {
      overflow: visible;
      position: relative;
      z-index: 10;
    }

    .deal-summary-open app-hand-area {
      position: relative;
      z-index: 1;
    }

    .bid-dialog-open app-table-surface {
      position: relative;
      z-index: 52;
    }

    .bid-dialog-open app-hand-area {
      position: relative;
      z-index: 60;
    }

    .connection-banner {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.375rem 1rem;
      font-size: 0.75rem;
      font-weight: 500;
      z-index: 100;
    }

    .connection-banner.reconnecting {
      background: hsl(45 90% 55% / 0.15);
      color: hsl(45 90% 65%);
      border-bottom: 1px solid hsl(45 90% 55% / 0.3);
    }

    .connection-banner.disconnected,
    .connection-banner.error {
      background: hsl(0 72% 51% / 0.15);
      color: hsl(0 72% 65%);
      border-bottom: 1px solid hsl(0 72% 51% / 0.3);
    }

    .connection-spinner {
      width: 12px;
      height: 12px;
      border: 2px solid hsl(45 90% 55% / 0.3);
      border-top-color: hsl(45 90% 65%);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .retry-btn {
      padding: 0.125rem 0.5rem;
      font-size: 0.75rem;
      background: hsl(0 72% 51% / 0.2);
      color: hsl(0 72% 65%);
      border: 1px solid hsl(0 72% 51% / 0.4);
      border-radius: 4px;
      cursor: pointer;
      transition: background 0.15s ease;
    }

    .retry-btn:hover {
      background: hsl(0 72% 51% / 0.35);
    }

    .fullscreen-banner {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      font-size: 0.75rem;
      background: hsl(var(--primary) / 0.08);
      border-bottom: 1px solid hsl(var(--primary) / 0.2);
      animation: bannerSlideIn 0.3s ease;
    }

    @keyframes bannerSlideIn {
      from { opacity: 0; transform: translateY(-100%); }
      to { opacity: 1; transform: translateY(0); }
    }

    .fullscreen-banner-text {
      color: hsl(var(--muted-foreground));
      font-size: 0.6875rem;
    }

    .fullscreen-banner-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.1875rem 0.5rem;
      font-size: 0.6875rem;
      font-weight: 600;
      background: hsl(var(--primary));
      color: hsl(var(--primary-foreground));
      border: none;
      border-radius: 9999px;
      cursor: pointer;
      transition: opacity 0.15s ease;
      white-space: nowrap;
    }

    .fullscreen-banner-btn:hover {
      opacity: 0.85;
    }

    .fullscreen-banner-dismiss {
      padding: 0.125rem 0.375rem;
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground));
      background: none;
      border: none;
      cursor: pointer;
      transition: color 0.15s ease;
      white-space: nowrap;
    }

    .fullscreen-banner-dismiss:hover {
      color: hsl(var(--foreground));
    }

    /* ── Chat FAB ── */
    .chat-fab {
      position: fixed;
      bottom: 1.25rem;
      right: 1.25rem;
      z-index: 50;
      width: 3.25rem;
      height: 3.25rem;
      border-radius: 50%;
      border: none;
      background: hsl(var(--primary));
      color: hsl(var(--primary-foreground));
      display: grid;
      place-items: center;
      cursor: pointer;
      box-shadow:
        0 2px 8px hsl(0 0% 0% / 0.3),
        0 0 0 0.5px hsl(0 0% 0% / 0.1);
      transition: transform 0.2s cubic-bezier(0.2, 0, 0, 1), box-shadow 0.2s ease, opacity 0.2s ease;
      animation: fabIn 0.25s cubic-bezier(0.2, 0, 0, 1);
    }

    .chat-fab:hover {
      transform: scale(1.08);
      box-shadow:
        0 4px 16px hsl(0 0% 0% / 0.35),
        0 0 0 0.5px hsl(0 0% 0% / 0.1);
    }

    .chat-fab:active {
      transform: scale(0.95);
    }

    @keyframes fabIn {
      from {
        opacity: 0;
        transform: scale(0.5);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }

    .chat-fab-badge {
      position: absolute;
      top: -0.125rem;
      right: -0.125rem;
      min-width: 1.125rem;
      height: 1.125rem;
      padding: 0 0.3125rem;
      border-radius: 9999px;
      background: hsl(0 72% 51%);
      color: #fff;
      font-size: 0.625rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
      pointer-events: none;
      box-shadow: 0 1px 4px hsl(0 0% 0% / 0.25);
      animation: badgePop 0.2s cubic-bezier(0.2, 0, 0, 1);
    }

    @keyframes badgePop {
      from { transform: scale(0); }
      to { transform: scale(1); }
    }

    @media (max-width: 480px) {
      .chat-fab {
        bottom: 0.75rem;
        right: 0.75rem;
        width: 2.75rem;
        height: 2.75rem;
      }
      .achievements-fab {
        bottom: 0.5rem;
        left: 0.5rem;
        width: 2.25rem;
        height: 2.25rem;
      }
    }

    /* ── Achievements FAB ── */
    .achievements-fab {
      position: fixed;
      bottom: 0.625rem;
      left: 0.625rem;
      z-index: 50;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      border: none;
      background: hsl(var(--gold));
      color: hsl(220 20% 10%);
      display: grid;
      place-items: center;
      cursor: pointer;
      box-shadow:
        0 2px 12px hsl(var(--gold) / 0.4),
        0 0 20px hsl(var(--gold) / 0.15);
      transition: transform 0.2s cubic-bezier(0.2, 0, 0, 1), box-shadow 0.2s ease;
      animation: achFabIn 0.5s cubic-bezier(0.2, 0, 0, 1);
    }

    .achievements-fab::after {
      content: '';
      position: absolute;
      inset: -2px;
      border-radius: 50%;
      border: 1.5px solid hsl(var(--gold) / 0.4);
      animation: achPulse 2s ease-in-out infinite;
      pointer-events: none;
    }

    .achievements-fab:hover {
      transform: scale(1.1);
      box-shadow:
        0 4px 20px hsl(var(--gold) / 0.5),
        0 0 30px hsl(var(--gold) / 0.2);
    }

    .achievements-fab:active {
      transform: scale(0.95);
    }

    @keyframes achFabIn {
      0% { opacity: 0; transform: scale(0) rotate(-180deg); }
      60% { transform: scale(1.15) rotate(10deg); }
      100% { opacity: 1; transform: scale(1) rotate(0deg); }
    }

    @keyframes achPulse {
      0%, 100% { opacity: 0.5; transform: scale(1); }
      50% { opacity: 0; transform: scale(1.25); }
    }

    .achievements-fab-badge {
      position: absolute;
      top: -0.1875rem;
      right: -0.1875rem;
      min-width: 0.9375rem;
      height: 0.9375rem;
      padding: 0 0.25rem;
      border-radius: 9999px;
      background: hsl(var(--primary));
      color: #fff;
      font-size: 0.5625rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
      pointer-events: none;
      box-shadow: 0 1px 4px hsl(0 0% 0% / 0.25);
      animation: badgePop 0.2s cubic-bezier(0.2, 0, 0, 1) 0.4s both;
    }

    /* ── Achievements Popup ── */
    .achievements-popup-backdrop {
      position: fixed;
      inset: 0;
      z-index: 110;
      display: flex;
      align-items: flex-end;
      justify-content: flex-start;
      padding: 1.25rem;
      animation: fadeIn 0.15s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .achievements-popup {
      width: 320px;
      max-height: 60vh;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      box-shadow: 0 8px 32px hsl(0 0% 0% / 0.4);
      backdrop-filter: blur(40px) saturate(1.8);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      animation: achPopupIn 0.25s cubic-bezier(0.2, 0, 0, 1);
    }

    @keyframes achPopupIn {
      from { opacity: 0; transform: translateY(8px) scale(0.97); }
      to { opacity: 1; transform: translateY(0) scale(1); }
    }

    .achievements-popup-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      border-bottom: 1px solid hsl(var(--border));
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--gold));
    }

    .achievements-popup-close {
      margin-left: auto;
      background: none;
      border: none;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 0.375rem;
      display: grid;
      place-items: center;
      transition: color 0.15s ease, background 0.15s ease;
    }

    .achievements-popup-close:hover {
      color: hsl(var(--foreground));
      background: hsl(var(--muted) / 0.5);
    }

    .achievements-popup-list {
      padding: 0.5rem;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .ach-item {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      padding: 0.5rem 0.625rem;
      border-radius: 0.625rem;
      background: hsl(var(--secondary) / 0.5);
      border: 1px solid hsl(var(--border) / 0.5);
      animation: achItemIn 0.3s ease both;
      cursor: pointer;
      transition: background 0.15s ease;
    }

    .ach-item:hover {
      background: hsl(var(--secondary) / 0.8);
    }

    .ach-item.expanded {
      align-items: flex-start;
    }

    .ach-item.tier-high {
      border-color: hsl(var(--gold) / 0.4);
      background: hsl(var(--gold) / 0.08);
      box-shadow: 0 0 12px hsl(var(--gold) / 0.1);
    }

    .ach-item.tier-mid {
      border-color: hsl(var(--gold) / 0.25);
    }

    .ach-item.hidden-ach {
      border-style: dashed;
    }

    @keyframes achItemIn {
      from { opacity: 0; transform: translateX(-8px); }
      to { opacity: 1; transform: translateX(0); }
    }

    .ach-info {
      display: flex;
      flex-direction: column;
      gap: 0.1875rem;
      min-width: 0;
      flex: 1;
    }

    .ach-name {
      font-size: 0.8125rem;
      font-weight: 500;
      color: hsl(var(--foreground));
    }

    .ach-stars {
      display: flex;
      gap: 1px;
    }

    .ach-star {
      font-size: 0.6875rem;
      line-height: 1;
    }

    .ach-star.filled {
      color: hsl(var(--gold));
      animation: starPop 0.3s cubic-bezier(0.2, 0, 0, 1) both;
    }

    .ach-star.empty {
      color: hsl(var(--muted) / 0.6);
    }

    @keyframes starPop {
      0% { transform: scale(0); opacity: 0; }
      60% { transform: scale(1.3); }
      100% { transform: scale(1); opacity: 1; }
    }

    .ach-hidden-text {
      color: hsl(var(--muted-foreground));
      font-style: italic;
    }

    .ach-desc {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      line-height: 1.4;
      margin-top: 0.25rem;
      animation: descIn 0.2s ease;
    }

    @keyframes descIn {
      from { opacity: 0; max-height: 0; }
      to { opacity: 1; max-height: 4rem; }
    }

    @media (max-width: 480px) {
      .achievements-popup-backdrop {
        padding: 0.75rem;
      }
      .achievements-popup {
        width: 280px;
      }
    }
  `],
})
export class TableComponent implements OnInit, OnDestroy {
  readonly gameState = inject(GameStateService);
  readonly hub = inject(GameHubService);
  readonly chatService = inject(ChatService);
  private readonly session = inject(ClientSessionService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);
  readonly errorBanner = inject(ErrorBannerService);
  private readonly fullscreenService = inject(FullscreenService);
  private readonly sound = inject(SoundService);

  readonly MaximizeIcon = Maximize;
  readonly MessageCircleIcon = MessageCircle;
  readonly AwardIcon = Award;
  readonly XIcon = X;

  readonly profilePopupData = signal<PlayerProfileResponse | null>(null);
  readonly profilePopupTeam = signal<'team1' | 'team2'>('team1');
  readonly waitingForContinue = signal(false);
  readonly showNegotiationHistory = signal(false);
  readonly showWonTricks = signal(false);
  readonly showCutInfo = signal(false);
  readonly cutOutcome = signal<CutOutcome | null>(null);
  readonly showMatchHistory = signal(false);
  readonly showFullscreenSuggestion = signal(false);
  readonly showAchievementsPopup = signal(false);
  readonly expandedAchievement = signal<string | null>(null);

  /** True while the local user is playing an active match (leaving = abandoning) */
  readonly gameInProgress = computed(() => {
    const phase = this.gameState.phase();
    return !!this.gameState.gameId()
      && phase !== 'waiting'
      && phase !== 'matchEnd'
      && !this.session.isWatcher();
  });

  readonly myAchievements = computed(() => {
    const pos = this.gameState.myPosition();
    const all = this.gameState.earnedAchievements();
    if (!pos || !all.length) return [];
    return all.filter(a => a.playerPosition === pos).sort((a, b) => b.tier - a.tier);
  });

  readonly gameModePopup = signal<{
    mode: GameMode;
    multiplier: MultiplierState;
  } | null>(null);
  private gameModePopupTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private previousPhase: GamePhase | null = null;

  private readonly beforeUnloadHandler = (e: BeforeUnloadEvent) => {
    if (this.gameInProgress()) {
      e.preventDefault();
    }
  };

  constructor() {
    // A player abandoned the match — tell the remaining players/watchers why
    // the game just ended (the abandoner is already navigating home).
    this.hub.matchAbandoned$.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event.abandoner !== this.gameState.myPosition()) {
        this.errorBanner.show(this.transloco.translate('table.matchAbandoned'));
      }
    });

    // Watch for kick — navigate home
    effect(() => {
      if (this.gameState.wasKicked()) {
        this.gameState.leaveRoom();
        this.session.leaveRoom();
        this.router.navigate(['/']);
      }
    });

    // Watch for idle close — navigate home
    effect(() => {
      if (this.gameState.roomIdleClosed()) {
        this.gameState.leaveRoom();
        this.session.leaveRoom();
        this.router.navigate(['/']);
      }
    });

    // Close popup when bid dialog opens
    effect(() => {
      if (this.gameState.isMyTurn()) {
        this.profilePopupData.set(null);
      }
    });

    // Reset waiting state when a new deal/phase starts
    effect(() => {
      const phase = this.gameState.phase();
      if (phase !== 'dealSummary' && phase !== 'matchEnd') {
        this.waitingForContinue.set(false);
      }
    });

    // Log achievements when earned
    effect(() => {
      const achs = this.myAchievements();
      if (achs.length > 0) {
        console.log(`[Table] 🏆 ${achs.length} achievement(s) earned:`, achs.map(a => `${a.name} (tier ${a.tier})`));
      }
    });

    // Show game mode popup when negotiation ends
    effect(() => {
      const phase = this.gameState.phase();
      const prevPhase = this.previousPhase;
      this.previousPhase = phase;

      if (prevPhase === 'negotiation' && phase === 'playing') {
        this.sound.play('card_folding_started');
        const mode = this.gameState.gameMode();
        if (mode) {
          this.showGameModePopup(mode, this.gameState.multiplier());
        }
      }
    });
  }

  private showGameModePopup(
    mode: GameMode,
    multiplier: MultiplierState
  ): void {
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
    }
    this.gameModePopup.set({ mode, multiplier });
    this.gameModePopupTimeoutId = setTimeout(() => {
      this.gameModePopup.set(null);
      this.gameModePopupTimeoutId = null;
    }, 1250);
  }

  onModeBadgeClicked(): void {
    this.showNegotiationHistory.set(true);
  }

  onMatchPointsClicked(): void {
    this.showMatchHistory.set(true);
  }

  dismissGameModePopup(): void {
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
      this.gameModePopupTimeoutId = null;
    }
    this.gameModePopup.set(null);
  }

  acceptFullscreen(): void {
    this.showFullscreenSuggestion.set(false);
    this.fullscreenService.dismissPrompt();
    this.fullscreenService.enterFullscreen();
  }

  dismissFullscreen(): void {
    this.showFullscreenSuggestion.set(false);
    this.fullscreenService.dismissPrompt();
  }

  ngOnInit(): void {
    window.addEventListener('beforeunload', this.beforeUnloadHandler);

    // Show fullscreen suggestion on mobile (once)
    if (this.fullscreenService.shouldPrompt()) {
      this.showFullscreenSuggestion.set(true);
    }

    const roomId = this.route.snapshot.paramMap.get('roomId');
    const inviteToken = this.route.snapshot.queryParamMap.get('invite');
    const quickstart = this.route.snapshot.queryParamMap.get('quickstart') === 'true';

    // Quick game: room already loaded from home, auto-start
    if (quickstart && this.gameState.currentRoom()) {
      this.onStartGame();
      // Remove query param to avoid re-trigger on refresh
      this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    }

    if (roomId && !this.gameState.currentRoom()) {
      if (inviteToken && !this.session.clientId()) {
        // Invite flow: join via invite token
        this.api.joinRoom(roomId, undefined, inviteToken).subscribe({
          next: async (response) => {
            if (response.position) {
              this.session.joinRoom(roomId, response.clientId, response.position);
            }
            await this.gameState.enterRoom(response.room, false);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      } else if (!this.session.clientId()) {
        // Rejoin flow: authenticated user without a clientId (new tab / device)
        this.api.rejoinRoom(roomId).subscribe({
          next: async (response) => {
            if (response.position) {
              this.session.joinRoom(roomId, response.clientId, response.position);
            }
            await this.gameState.enterRoom(response.room, response.room.isOwner);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      } else {
        // Normal flow: fetch room and initialize
        this.api.getRoom(roomId).subscribe({
          next: async (room) => {
            const isCreator = room.isOwner;
            await this.gameState.enterRoom(room, isCreator);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      }
    }
  }

  ngOnDestroy(): void {
    window.removeEventListener('beforeunload', this.beforeUnloadHandler);
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
    }
  }

  onStartGame(): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    const clientId = this.session.clientId();
    const isCreator = this.gameState.isCreator();

    console.log('[Table] Starting game', {
      roomId,
      clientId,
      isCreator,
      playerName: this.session.playerName(),
      sessionSnapshot: this.session.getSession(),
    });

    if (roomId && clientId) {
      this.api.startGame(roomId, clientId).subscribe({
        next: async (response) => {
          console.log('[Table] Game started', response);
          this.gameState.setGameId(response.gameId);
          // Immediately refresh state after game starts (don't wait for SignalR)
          await this.gameState.refreshState();
        },
        error: () => {
          // Toast shown by ApiService
        },
      });
    } else {
      console.warn('[Table] Cannot start game - missing roomId or clientId', { roomId, clientId });
    }
  }

  onSubmitCut(position: number): void {
    if (this.gameState.isSubmittingAction()) return;
    this.sound.play('general_click');

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();
    if (!gameId || !clientId) return;

    this.cutOutcome.set(null);
    this.gameState.beginSubmit();
    // Keep the cutter on the cut stage while the deck animation plays
    this.gameState.holdCutStage();
    this.api.submitCut(gameId, clientId, position, true).subscribe({
      next: (res) => this.cutOutcome.set({ status: 'confirmed', position: res.position }),
      error: (err) => {
        console.error('Failed to submit cut', err);
        this.cutOutcome.set({ status: 'error' });
        this.gameState.releaseCutStage();
        this.gameState.endSubmit();
      },
    });
  }

  onCutAnimationDone(): void {
    this.cutOutcome.set(null);
    this.gameState.releaseCutStage();
    this.gameState.endSubmit();
  }

  onPlayCard(card: { rank: string; suit: string }): void {
    if (this.gameState.showingCompletedTrick()) {
      this.gameState.dismissCompletedTrick();
    }
    if (this.gameState.isSubmittingAction()) return;

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Playing card', { card, gameId, clientId });
    if (gameId && clientId) {
      this.gameState.beginSubmit();
      this.api.playCard(gameId, clientId, card.rank as any, card.suit as any).subscribe({
        next: () => {
          console.log('[Table] Card played successfully');
          this.gameState.endSubmit();
        },
        error: (err) => {
          console.error('Failed to play card', err);
          this.gameState.endSubmit();
          // Refresh state to get true server state
          this.gameState.refreshState();
        },
      });
    } else {
      console.warn('[Table] Cannot play card - missing gameId or clientId');
    }
  }

  onSubmitNegotiation(action: { actionType: string; mode?: string | null }): void {
    if (this.gameState.isSubmittingAction()) return;
    this.sound.play('general_click');

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Submitting negotiation', { action, gameId, clientId });
    if (gameId && clientId) {
      this.gameState.beginSubmit();
      this.api
        .submitNegotiation(
          gameId,
          clientId,
          action.actionType as any,
          action.mode as any
        )
        .subscribe({
          next: () => {
            console.log('[Table] Negotiation submitted successfully');
            this.gameState.endSubmit();
          },
          error: (err) => {
            console.error('Failed to submit negotiation', err);
            this.gameState.endSubmit();
            this.gameState.refreshState();
          },
        });
    } else {
      console.warn('[Table] Cannot submit negotiation - missing gameId or clientId');
    }
  }

  async onRetryConnection(): Promise<void> {
    try {
      await this.hub.connect(environment.hubUrl);
      const roomId = this.gameState.currentRoom()?.roomId;
      const clientId = this.session.clientId();
      if (roomId && clientId) {
        await this.hub.joinRoom(roomId, clientId);
      }
      await this.gameState.refreshState();
    } catch (e) {
      console.error('Failed to retry connection', e);
    }
  }

  onHideDealSummary(): void {
    this.sound.play('general_click');
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();
    const pendingAction = this.gameState.pendingActionType();

    // If waiting for ContinueDeal confirmation, call the API
    // The DealStarted SignalR event will hide the summary
    if (pendingAction === PendingActionType.ContinueDeal && gameId && clientId) {
      console.log('[Table] Submitting continue deal confirmation');
      this.waitingForContinue.set(true);
      this.api.submitContinueDeal(gameId, clientId).subscribe({
        next: () => {
          console.log('[Table] Continue deal submitted successfully');
          // Don't hide summary here - wait for DealStarted event
        },
        error: (err) => {
          console.error('Failed to submit continue deal', err);
          this.waitingForContinue.set(false);
          // On error, still hide the summary to not block the UI
          this.gameState.hideDealSummary();
        },
      });
    } else {
      // No ContinueDeal action pending (e.g., watcher or AI game)
      // Just hide the summary immediately
      this.gameState.hideDealSummary();
    }
  }

  onDismissCompletedTrick(): void {
    this.sound.play('general_click');
    this.gameState.dismissCompletedTrick();
  }

  async onPlayAgain(): Promise<void> {
    this.sound.play('general_click');
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    // Always attempt the submit — the server validates the pending action.
    // Relying on the client's cached pendingActionType made the click a
    // silent no-op whenever the client was out of sync with the server.
    if (gameId && clientId) {
      this.waitingForContinue.set(true);
      this.api.submitContinueMatch(gameId, clientId).subscribe({
        error: (err) => {
          console.error('Failed to submit continue match', err);
          this.waitingForContinue.set(false);
          this.errorBanner.show(this.transloco.translate('matchEnd.playAgainFailed'));
          // Re-sync with the server (the play-again window may have closed)
          this.gameState.refreshState();
        },
      });
    }
  }

  onSetSeatMode(event: { position: PlayerPosition; accessMode: SeatAccessMode }): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (roomId) {
      this.api.setSeatMode(roomId, event.position, event.accessMode).subscribe({
        error: (err) => console.error('Failed to set seat mode', err),
      });
    }
  }

  onGenerateInvite(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (!roomId) return;

    this.api.generateInvite(roomId, position).subscribe({
      next: (response) => {
        const inviteUrl = `${window.location.origin}/table/${roomId}?invite=${response.token}`;
        this.shareOrCopy(inviteUrl);
      },
      error: (err) => console.error('Failed to generate invite', err),
    });
  }

  private async shareOrCopy(url: string): Promise<void> {
    if (navigator.share) {
      try {
        await navigator.share({
          title: this.transloco.translate('waiting.shareTitle'),
          text: this.transloco.translate('waiting.shareText'),
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
      this.errorBanner.show(this.transloco.translate('waiting.linkCopied'));
    } catch {
      this.errorBanner.show(this.transloco.translate('waiting.linkCopyFailed'));
    }
  }

  onKickPlayer(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (roomId) {
      this.api.kickPlayer(roomId, position).subscribe({
        error: (err) => console.error('Failed to kick player', err),
      });
    }
  }

  onSeatClicked(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (!roomId) return;

    this.api.getPlayerProfile(roomId, position).subscribe({
      next: (profile) => {
        const team = getTeam(position);
        this.profilePopupTeam.set(team === 'Team1' ? 'team1' : 'team2');
        this.profilePopupData.set(profile);
      },
    });
  }

  onTricksClicked(position: PlayerPosition): void {
    // Only the viewer's own team's won tricks may be inspected
    const myTeam = this.gameState.myTeam();
    if (this.gameState.isWatcher() || !myTeam || getTeam(position) !== myTeam) return;
    this.showWonTricks.set(true);
  }

  onChatClicked(): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (roomId) {
      this.chatService.openPopup(roomId);
    }
  }

  onChatMessageSent(content: string): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    const clientId = this.session.clientId();
    if (roomId && clientId) {
      this.chatService.sendMessage(roomId, clientId, content);
    }
  }

  closeProfilePopup(): void {
    this.profilePopupData.set(null);
  }

  tierDots(tier: number): number[] {
    return Array.from({ length: Math.min(tier, 5) });
  }

  toggleAchievementExpand(code: string): void {
    this.expandedAchievement.update(c => c === code ? null : code);
  }

  async onLeaveTable(): Promise<void> {
    // The canDeactivate guard handles the confirmation dialog and session cleanup.
    // For the explicit "Leave Table" button, also call the leave API before navigating.
    // Leaving an active match abandons it (the server forfeits the game).
    if (this.gameInProgress() && !confirm(this.transloco.translate('table.leaveConfirm'))) {
      return;
    }

    const roomId = this.gameState.currentRoom()?.roomId;
    const clientId = this.session.clientId();

    if (roomId && clientId) {
      try {
        await this.api.leaveRoom(roomId, clientId).toPromise();
      } catch (e) {
        console.warn('Failed to leave room via API', e);
      }
    }

    this.chatService.reset();
    await this.gameState.leaveRoom();
    this.session.leaveRoom();
    this.router.navigate(['/']);
  }
}
