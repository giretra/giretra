import { Component, input, output, computed } from '@angular/core';
import { PlayerProfileResponse } from '../../../core/services/api.service';
import {
  LucideAngularModule,
  Trophy,
  Flame,
  Calendar,
  Star,
  EyeOff,
  Bot,
  X,
  Github,
} from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-player-profile-popup',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop" (click)="closed.emit()"></div>
    <div class="popup-container" (click)="closed.emit()">
      <div class="popup-panel" [class]="teamClass()" (click)="$event.stopPropagation()">
        <!-- Close button -->
        <button class="close-btn" (click)="closed.emit()">
          <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>

        @if (!profile().isBot) {
          <!-- HUMAN PROFILE -->
          <div class="profile-header">
            <div class="avatar-circle" [class]="teamClass()">
              @if (profile().avatarUrl) {
                <img [src]="profile().avatarUrl" alt="" class="avatar-img" />
              } @else {
                <span class="avatar-initial">{{ initial() }}</span>
              }
            </div>
            <div class="display-name">{{ profile().displayName }}</div>
            @if (milestone()) {
              <span class="milestone-label">{{ t('playerProfile.' + milestone()) }}</span>
            }
          </div>

          @if (profile().memberSince) {
            <div class="member-since">
              <i-lucide [img]="CalendarIcon" [size]="14" [strokeWidth]="2"></i-lucide>
              <span>{{ t('playerProfile.playingSince', { date: memberSinceText() }) }}</span>
            </div>
          }

          <div class="stats-grid">
            <div class="stat-cell">
              <span class="stat-value">{{ profile().gamesPlayed }}</span>
              <span class="stat-label">{{ t('playerProfile.played') }}</span>
            </div>
            <div class="stat-cell">
              @if (profile().eloRating != null) {
                <span class="stat-value elo-value">{{ profile().eloRating }}</span>
              } @else {
                <span class="stat-value elo-private-value">
                  <i-lucide [img]="EyeOffIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </span>
              }
              <span class="stat-label">{{ t('playerProfile.elo') }}</span>
            </div>
            <div class="stat-cell">
              <span class="stat-value streak-value">
                {{ profile().winStreak }}
                @if (profile().winStreak >= 3) {
                  <i-lucide [img]="FlameIcon" [size]="14" [strokeWidth]="2" class="flame-icon"></i-lucide>
                }
              </span>
              <span class="stat-label">{{ t('playerProfile.streak') }}</span>
            </div>
            <div class="stat-cell">
              <span class="stat-value best-streak-value">
                {{ profile().bestWinStreak }}
                @if (profile().bestWinStreak >= 5) {
                  <i-lucide [img]="TrophyIcon" [size]="14" [strokeWidth]="2" class="trophy-icon"></i-lucide>
                }
              </span>
              <span class="stat-label">{{ t('playerProfile.best') }}</span>
            </div>
          </div>
        } @else {
          <!-- BOT PROFILE -->
          <div class="profile-header">
            <div class="avatar-circle bot" [class]="teamClass()">
              <i-lucide [img]="BotIcon" [size]="28" [strokeWidth]="1.5"></i-lucide>
            </div>
            <div class="display-name">{{ profile().displayName }}</div>
          </div>

          @if (profile().difficulty != null) {
            <div class="difficulty-row">
              @for (s of difficultyStars(); track s) {
                <i-lucide [img]="StarIcon" [size]="16" [strokeWidth]="2" class="star-icon filled"></i-lucide>
              }
              @for (s of emptyStars(); track s) {
                <i-lucide [img]="StarIcon" [size]="16" [strokeWidth]="2" class="star-icon empty"></i-lucide>
              }
            </div>
          }

          @if (profile().pun) {
            <div class="pun-quote">"{{ profile().pun }}"</div>
          }

          @if (botTypeKey()) {
            <div class="bot-type-section">
              <span class="bot-type-badge">{{ t('playerProfile.botType.' + botTypeKey()) }}</span>
              <span class="bot-type-desc">{{ t('playerProfile.botType.' + botTypeKey() + 'Desc') }}</span>
            </div>
          }

          @if (profile().description) {
            <p class="bot-description">{{ profile().description }}</p>
          }

          <div class="info-row">
            <div class="info-item">
              <span class="info-label">{{ t('playerProfile.author') }}</span>
              <span class="info-value">{{ profile().author || t('playerProfile.builtIn') }}</span>
            </div>
            <a class="github-link" [href]="githubUrl()" target="_blank" rel="noopener noreferrer">
              <i-lucide [img]="GithubIcon" [size]="14" [strokeWidth]="2"></i-lucide>
              <span>{{ t('playerProfile.viewOnGithub') }}</span>
            </a>
          </div>

          <div class="bot-rating-section">
            <span class="stat-value elo-value">{{ profile().botRating ?? '—' }}</span>
            <span class="stat-label">{{ t('playerProfile.rating') }}</span>
          </div>
        }
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 100;
      background: rgba(0, 0, 0, 0.5);
      animation: fadeIn 0.2s ease;
    }

    .popup-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 110;
      pointer-events: none;
    }

    .popup-panel {
      pointer-events: auto;
      position: relative;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 1.5rem;
      max-width: 340px;
      width: calc(100% - 2rem);
      animation: scaleIn 0.25s ease;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
    }

    .close-btn {
      position: absolute;
      top: 0.75rem;
      right: 0.75rem;
      background: none;
      border: none;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 0.25rem;
      transition: color 0.15s ease;
    }

    .close-btn:hover {
      color: hsl(var(--foreground));
    }

    /* Profile header */
    .profile-header {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
    }

    .avatar-circle {
      width: 4rem;
      height: 4rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      display: flex;
      align-items: center;
      justify-content: center;
      border: 3px solid hsl(var(--border));
      overflow: hidden;
    }

    .avatar-circle.team1 {
      border-color: hsl(var(--team1));
    }

    .avatar-circle.team2 {
      border-color: hsl(var(--team2));
    }

    .avatar-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .avatar-initial {
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      text-transform: uppercase;
    }

    .avatar-circle.bot {
      color: hsl(var(--muted-foreground));
    }

    .display-name {
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      text-align: center;
    }

    .milestone-label {
      font-size: 0.625rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
    }

    /* Member since */
    .member-since {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    /* Difficulty */
    .difficulty-row {
      display: flex;
      align-items: center;
      gap: 0.125rem;
    }

    .star-icon.filled {
      color: hsl(var(--gold));
    }

    .star-icon.empty {
      color: hsl(var(--muted-foreground) / 0.3);
    }

    /* Bot pun */
    .pun-quote {
      font-style: italic;
      font-size: 0.8125rem;
      color: hsl(var(--gold));
      text-align: center;
      line-height: 1.4;
    }

    /* Bot type */
    .bot-type-section {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
    }

    .bot-type-badge {
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      background: hsl(var(--muted));
      color: hsl(var(--foreground));
    }

    .bot-type-desc {
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .bot-description {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
      margin: 0;
      line-height: 1.4;
    }

    /* Info row */
    .info-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      width: 100%;
      padding-top: 0.5rem;
      border-top: 1px solid hsl(var(--border));
    }

    .info-item {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
    }

    .info-label {
      font-size: 0.625rem;
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .info-value {
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--foreground));
    }

    .github-link {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-decoration: none;
      transition: color 0.15s ease;
    }

    .github-link:hover {
      color: hsl(var(--foreground));
    }

    /* Bot rating */
    .bot-rating-section {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.125rem;
      width: 100%;
      padding-top: 0.5rem;
      border-top: 1px solid hsl(var(--border));
    }

    /* Stats grid */
    .stats-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem;
      width: 100%;
      padding-top: 0.5rem;
      border-top: 1px solid hsl(var(--border));
    }

    .stat-cell {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.125rem;
      padding: 0.375rem;
    }

    .stat-value {
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      font-variant-numeric: tabular-nums;
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .stat-label {
      font-size: 0.625rem;
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    /* ELO value */
    .elo-value {
      color: hsl(var(--gold));
    }

    .elo-private-value {
      color: hsl(var(--muted-foreground));
    }

    /* Streak icons */
    .flame-icon {
      color: hsl(20, 90%, 55%);
    }

    .trophy-icon {
      color: hsl(var(--gold));
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.9);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }
  `],
})
export class PlayerProfilePopupComponent {
  readonly XIcon = X;
  readonly TrophyIcon = Trophy;
  readonly FlameIcon = Flame;
  readonly CalendarIcon = Calendar;
  readonly StarIcon = Star;
  readonly EyeOffIcon = EyeOff;
  readonly BotIcon = Bot;
  readonly GithubIcon = Github;

  readonly profile = input.required<PlayerProfileResponse>();
  readonly teamClass = input<'team1' | 'team2'>('team1');

  readonly closed = output<void>();

  readonly initial = computed(() => {
    const name = this.profile().displayName;
    return name ? name.charAt(0).toUpperCase() : '?';
  });

  readonly milestone = computed(() => {
    const played = this.profile().gamesPlayed;
    if (played >= 500) return 'expert';
    if (played >= 100) return 'veteran';
    return null;
  });

  readonly memberSinceText = computed(() => {
    const ms = this.profile().memberSince;
    if (!ms) return '';
    const date = new Date(ms);
    return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
  });

  readonly difficultyStars = computed(() => {
    const d = this.profile().difficulty ?? 0;
    return Array.from({ length: d }, (_, i) => i);
  });

  readonly emptyStars = computed(() => {
    const d = this.profile().difficulty ?? 0;
    return Array.from({ length: Math.max(0, 3 - d) }, (_, i) => i);
  });

  readonly isBuiltIn = computed(() => !this.profile().author);

  readonly githubUrl = computed(
    () => this.profile().authorGithubUrl || 'https://github.com/giretra/giretra',
  );

  readonly botTypeKey = computed(() => {
    const bt = this.profile().botType;
    if (bt === 'deterministic') return 'deterministic';
    if (bt === 'ml') return 'ml';
    return null;
  });
}
