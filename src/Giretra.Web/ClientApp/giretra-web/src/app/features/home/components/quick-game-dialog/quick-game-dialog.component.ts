import { Component, input, output, signal } from '@angular/core';
import { LucideAngularModule, X, Trophy, Bot } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { AiTypeInfo } from '../../../../core/services/api.service';

@Component({
  selector: 'app-quick-game-dialog',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      @if (open()) {
        <div class="backdrop" (click)="closed.emit()"></div>
        <div class="dialog-container" (click)="closed.emit()">
          <div class="dialog" (click)="$event.stopPropagation()">
            <!-- Header -->
            <div class="dialog-header">
              <div class="dialog-title-row">
                <i-lucide [img]="BotIcon" [size]="18" [strokeWidth]="2"></i-lucide>
                <h2 class="dialog-title">{{ t('quickGame.title') }}</h2>
              </div>
              <button class="close-btn" (click)="closed.emit()">
                <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
              </button>
            </div>

            <!-- Bot selector -->
            <p class="section-label">{{ t('quickGame.chooseDifficulty') }}</p>
            <div class="bot-list">
              @for (bot of aiTypes(); track bot.name) {
                <button
                  class="bot-row"
                  [class.selected]="selectedBot() === bot.name"
                  (click)="selectedBot.set(bot.name)"
                >
                  <span class="bot-info">
                    <span class="bot-name">{{ bot.displayName }}</span>
                    @if (bot.pun) {
                      <span class="bot-pun">{{ bot.pun }}</span>
                    }
                  </span>
                  <span class="bot-difficulty">
                    @for (dot of difficultyDots(bot.difficulty); track $index) {
                      <span class="dot" [class.filled]="dot"></span>
                    }
                  </span>
                  <span class="bot-elo">{{ bot.rating }}</span>
                </button>
              }
            </div>

            <!-- Info line -->
            <div class="info-line">
              <span>{{ t('quickGame.turnTimer') }}</span>
            </div>

            <!-- Rated toggle -->
            <div class="rated-row">
              <button
                type="button"
                class="rated-subtle"
                [class.active]="isRanked()"
                (click)="isRanked.set(!isRanked())"
              >
                <i-lucide [img]="TrophyIcon" [size]="10" [strokeWidth]="1.5"></i-lucide>
                <span>{{ isRanked() ? t('quickGame.rated') : t('quickGame.unrated') }}</span>
                <span class="toggle-track-sm">
                  <span class="toggle-thumb-sm"></span>
                </span>
              </button>
            </div>

            <!-- Play button -->
            <button
              class="play-btn"
              [disabled]="!selectedBot()"
              (click)="onPlay()"
            >
              {{ t('quickGame.play') }}
            </button>

            <!-- Create room link -->
            <button class="custom-game-link" (click)="createRoom.emit()">
              {{ t('quickGame.customGame') }}
            </button>
          </div>
        </div>
      }
    </ng-container>
  `,
  styles: [`
    :host { display: contents; }

    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 50;
      animation: fadeIn 0.3s ease;
    }

    .dialog-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 110;
    }

    .dialog {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 1.5rem;
      min-width: 300px;
      max-width: 480px;
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 1rem;
      animation: scaleIn 0.25s ease;
    }

    .dialog-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .dialog-title-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: hsl(var(--foreground));
    }

    .dialog-title {
      font-size: 1.125rem;
      font-weight: 700;
      margin: 0;
      color: hsl(var(--foreground));
    }

    .close-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 50%;
      border: none;
      background: transparent;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .close-btn:hover {
      color: hsl(var(--foreground));
      background: hsl(var(--foreground) / 0.1);
    }

    .section-label {
      font-size: 0.75rem;
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.06em;
    }

    .bot-list {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      max-height: 14rem;
      overflow-y: auto;
      scrollbar-width: thin;
      scrollbar-color: hsl(var(--muted) / 0.5) transparent;
    }

    .bot-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      width: 100%;
      padding: 0.625rem 0.875rem;
      background: hsl(var(--secondary));
      border: 2px solid hsl(var(--border));
      border-radius: 0.625rem;
      cursor: pointer;
      transition: all 0.15s ease;
      color: inherit;
      text-align: left;
    }
    .bot-row:hover {
      border-color: hsl(var(--foreground) / 0.25);
      background: hsl(var(--foreground) / 0.04);
    }
    .bot-row.selected {
      border-color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.08);
      box-shadow: 0 0 12px hsl(var(--gold) / 0.15);
    }

    .bot-info {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      flex: 1;
      min-width: 0;
    }

    .bot-name {
      font-size: 0.875rem;
      font-weight: 600;
      color: hsl(var(--foreground));
    }

    .bot-pun {
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground) / 0.7);
      font-style: italic;
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .bot-difficulty {
      display: flex;
      gap: 0.25rem;
      flex-shrink: 0;
    }

    .dot {
      width: 0.4375rem;
      height: 0.4375rem;
      border-radius: 50%;
      background: hsl(var(--muted) / 0.5);
      transition: background 0.15s ease;
    }
    .dot.filled {
      background: hsl(var(--gold));
    }

    .bot-elo {
      font-size: 0.75rem;
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      font-variant-numeric: tabular-nums;
      flex-shrink: 0;
      min-width: 2.5rem;
      text-align: right;
    }

    .info-line {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .rated-row {
      display: flex;
      align-items: center;
    }

    .rated-subtle {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      padding: 0.2rem 0;
      background: none;
      border: none;
      color: hsl(var(--muted-foreground) / 0.55);
      font-size: 0.6875rem;
      font-weight: 400;
      cursor: pointer;
      transition: color 0.15s ease;
    }
    .rated-subtle:hover {
      color: hsl(var(--muted-foreground));
    }
    .rated-subtle.active {
      color: hsl(var(--muted-foreground) / 0.8);
    }

    .toggle-track-sm {
      width: 1.375rem;
      height: 0.75rem;
      border-radius: 9999px;
      background: hsl(var(--muted) / 0.5);
      position: relative;
      transition: background 0.15s ease;
    }
    .rated-subtle.active .toggle-track-sm {
      background: hsl(var(--muted-foreground) / 0.4);
    }

    .toggle-thumb-sm {
      position: absolute;
      top: 0.1rem;
      left: 0.1rem;
      width: 0.55rem;
      height: 0.55rem;
      border-radius: 50%;
      background: hsl(var(--muted-foreground) / 0.4);
      transition: transform 0.15s ease;
    }
    .rated-subtle.active .toggle-thumb-sm {
      transform: translateX(0.625rem);
      background: hsl(var(--muted-foreground) / 0.7);
    }

    .play-btn {
      width: 100%;
      padding: 0.75rem;
      font-size: 1rem;
      font-weight: 700;
      letter-spacing: 0.02em;
      background: hsl(var(--gold));
      color: hsl(var(--accent-foreground));
      border: none;
      border-radius: 0.625rem;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .play-btn:hover:not(:disabled) {
      opacity: 0.9;
      transform: translateY(-1px);
      box-shadow: 0 4px 16px hsl(var(--gold) / 0.3);
    }
    .play-btn:active:not(:disabled) {
      transform: translateY(0);
    }
    .play-btn:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }

    .custom-game-link {
      background: none;
      border: none;
      color: hsl(var(--muted-foreground) / 0.6);
      font-size: 0.75rem;
      cursor: pointer;
      text-align: center;
      transition: color 0.15s ease;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    .custom-game-link:hover {
      color: hsl(var(--muted-foreground));
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes scaleIn {
      from { opacity: 0; transform: scale(0.9); }
      to { opacity: 1; transform: scale(1); }
    }

    @media (max-width: 480px) {
      .dialog {
        min-width: 0;
        padding: 1.25rem;
      }
    }
  `],
})
export class QuickGameDialogComponent {
  readonly XIcon = X;
  readonly TrophyIcon = Trophy;
  readonly BotIcon = Bot;

  readonly open = input<boolean>(false);
  readonly aiTypes = input<AiTypeInfo[]>([]);

  readonly play = output<{ aiType: string; isRanked: boolean }>();
  readonly closed = output<void>();
  readonly createRoom = output<void>();

  readonly selectedBot = signal<string>('');
  readonly isRanked = signal(true);

  difficultyDots(difficulty: number): boolean[] {
    const max = 3;
    return Array.from({ length: max }, (_, i) => i < difficulty);
  }

  onPlay(): void {
    const bot = this.selectedBot();
    if (bot) {
      this.play.emit({ aiType: bot, isRanked: this.isRanked() });
    }
  }
}
