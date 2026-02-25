import { Component, output, signal, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AiSeat, AiTypeInfo, ApiService, RoomResponse } from '../../../../core/services/api.service';
import { ClientSessionService } from '../../../../core/services/client-session.service';
import { AuthService } from '../../../../core/services/auth.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { LucideAngularModule, Bot, UserPlus, Lock, Trophy } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

const DEFAULT_AI_TYPE = 'DeterministicPlayer';

@Component({
  selector: 'app-create-room-form',
  standalone: true,
  imports: [FormsModule, HlmButton, LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    <div class="create-form">
      <div class="form-header">
        <h3>{{ t('createForm.title') }}</h3>
        <button
          type="button"
          class="close-btn"
          (click)="cancelled.emit()"
        >&times;</button>
      </div>

      <form (ngSubmit)="onSubmit()">
        <!-- Room name -->
        <div class="field">
          <label for="roomName" class="field-label">{{ t('createForm.tableName') }}</label>
          <input
            id="roomName"
            type="text"
            class="text-input"
            [(ngModel)]="roomName"
            name="roomName"
            [placeholder]="t('createForm.tableNamePlaceholder', { name: displayName() })"
            maxlength="50"
            [disabled]="submitting()"
          />
        </div>

        <!-- Bot seats -->
        <div class="field">
          <button
            type="button"
            class="fill-bots-toggle"
            [class.active]="allAi"
            [disabled]="submitting()"
            (click)="toggleAllAi()"
          >
            <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
            <span>{{ allAi ? t('createForm.clearAll') : t('createForm.fillAll') }}</span>
            <span class="toggle-track">
              <span class="toggle-thumb"></span>
            </span>
          </button>

          <!-- Visual compass seat selector -->
          <div class="seat-selector">
            <!-- Top (Partner) -->
            <div class="seat-row">
              <div class="seat-group">
                <button
                  type="button"
                  class="seat-btn"
                  [class.active]="aiSeats.Top"
                  [disabled]="submitting()"
                  (click)="toggleSeat('Top')"
                >
                  @if (aiSeats.Top) {
                    <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  }
                  <span class="seat-label">{{ t('positions.top') }}</span>
                  <span class="seat-role">{{ t('createForm.partner') }}</span>
                </button>
                @if (aiSeats.Top && aiTypes().length > 1) {
                  <select
                    class="ai-type-select"
                    [disabled]="submitting()"
                    [ngModel]="aiSeats.Top"
                    (ngModelChange)="aiSeats.Top = $event"
                    name="aiTypeTop"
                  >
                    @for (type of aiTypes(); track type.name) {
                      <option [value]="type.name">{{ type.displayName }} ({{ type.rating }})</option>
                    }
                  </select>
                }
              </div>
            </div>
            <!-- Middle: Left + You + Right -->
            <div class="seat-row middle">
              <div class="seat-group">
                <button
                  type="button"
                  class="seat-btn"
                  [class.active]="aiSeats.Left"
                  [disabled]="submitting()"
                  (click)="toggleSeat('Left')"
                >
                  @if (aiSeats.Left) {
                    <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  }
                  <span class="seat-label">{{ t('positions.left') }}</span>
                </button>
                @if (aiSeats.Left && aiTypes().length > 1) {
                  <select
                    class="ai-type-select"
                    [disabled]="submitting()"
                    [ngModel]="aiSeats.Left"
                    (ngModelChange)="aiSeats.Left = $event"
                    name="aiTypeLeft"
                  >
                    @for (type of aiTypes(); track type.name) {
                      <option [value]="type.name">{{ type.displayName }} ({{ type.rating }})</option>
                    }
                  </select>
                }
              </div>
              <div class="you-marker">{{ t('common.you') }}</div>
              <div class="seat-group">
                <button
                  type="button"
                  class="seat-btn"
                  [class.active]="aiSeats.Right"
                  [disabled]="submitting()"
                  (click)="toggleSeat('Right')"
                >
                  @if (aiSeats.Right) {
                    <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  }
                  <span class="seat-label">{{ t('positions.right') }}</span>
                </button>
                @if (aiSeats.Right && aiTypes().length > 1) {
                  <select
                    class="ai-type-select"
                    [disabled]="submitting()"
                    [ngModel]="aiSeats.Right"
                    (ngModelChange)="aiSeats.Right = $event"
                    name="aiTypeRight"
                  >
                    @for (type of aiTypes(); track type.name) {
                      <option [value]="type.name">{{ type.displayName }} ({{ type.rating }})</option>
                    }
                  </select>
                }
              </div>
            </div>
          </div>

          <p class="hint">{{ t('createForm.aiHint') }}</p>
        </div>

        <!-- Turn timer -->
        <div class="field">
          <label class="field-label">{{ t('createForm.turnTimer') }}</label>
          <div class="timer-presets">
            @for (preset of timerPresets; track preset.value) {
              <button
                type="button"
                class="timer-btn"
                [class.active]="selectedTimer === preset.value"
                [disabled]="submitting()"
                (click)="selectedTimer = preset.value"
              >
                {{ preset.label }}
              </button>
            }
          </div>
        </div>

        <!-- Invite only -->
        <div class="field">
          <button
            type="button"
            class="invite-only-toggle"
            [class.active]="inviteOnly"
            [disabled]="submitting()"
            (click)="inviteOnly = !inviteOnly"
          >
            <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2"></i-lucide>
            <span>{{ t('createForm.inviteOnly') }}</span>
            <span class="toggle-track">
              <span class="toggle-thumb"></span>
            </span>
          </button>
          <p class="hint">{{ t('createForm.inviteOnlyHint') }}</p>
        </div>

        <!-- Rated (subtle) -->
        <div class="rated-field">
          <button
            type="button"
            class="rated-subtle"
            [class.active]="isRanked"
            [disabled]="submitting()"
            (click)="isRanked = !isRanked"
          >
            <i-lucide [img]="TrophyIcon" [size]="10" [strokeWidth]="1.5"></i-lucide>
            <span>{{ t('createForm.rated') }}</span>
            <span class="toggle-track-sm">
              <span class="toggle-thumb-sm"></span>
            </span>
          </button>
        </div>

        @if (error()) {
          <p class="error-msg">{{ error() }}</p>
        }

        <div class="actions">
          <button
            type="button"
            class="cancel-btn"
            (click)="cancelled.emit()"
            [disabled]="submitting()"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            type="submit"
            hlmBtn
            variant="default"
            class="submit-btn"
            [disabled]="submitting()"
          >
            @if (submitting()) {
              {{ t('createForm.creating') }}
            } @else {
              {{ t('createForm.createTable') }}
            }
          </button>
        </div>
      </form>
    </div>
    </ng-container>
  `,
  styles: [`
    .create-form {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      overflow: hidden;
      animation: slideDown 0.2s ease;
    }

    @keyframes slideDown {
      from {
        opacity: 0;
        transform: translateY(-8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .form-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.875rem 1rem;
      border-bottom: 1px solid hsl(var(--border));
    }

    .form-header h3 {
      margin: 0;
      font-size: 1rem;
      font-weight: 700;
      color: hsl(var(--foreground));
    }

    .close-btn {
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 0.375rem;
      border: none;
      background: transparent;
      color: hsl(var(--muted-foreground));
      font-size: 1.25rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s ease;
    }

    .close-btn:hover {
      background: hsl(var(--muted) / 0.5);
      color: hsl(var(--foreground));
    }

    form {
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }

    /* Fields */
    .field {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .field-label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .text-input {
      width: 100%;
      padding: 0.5rem 0.75rem;
      font-size: 0.9375rem;
      background: hsl(var(--input));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--foreground));
      outline: none;
      transition: border-color 0.15s ease;
    }

    .text-input:focus {
      border-color: hsl(var(--primary));
    }

    .text-input::placeholder {
      color: hsl(var(--muted-foreground));
    }

    .text-input:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    /* Seat selector */
    .seat-selector {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
      padding: 0.75rem 0;
    }

    .seat-row {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
    }

    .seat-row.middle {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .seat-group {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
    }

    .seat-btn {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.125rem;
      padding: 0.5rem 0.75rem;
      min-width: 4rem;
      background: hsl(var(--muted) / 0.2);
      border: 1.5px dashed hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .seat-btn:hover:not(:disabled) {
      background: hsl(var(--muted) / 0.4);
      border-color: hsl(var(--muted-foreground));
    }

    .seat-btn.active {
      background: hsl(var(--gold) / 0.12);
      border-style: solid;
      border-color: hsl(var(--gold) / 0.5);
      color: hsl(var(--gold));
    }

    .seat-btn.active:hover:not(:disabled) {
      background: hsl(var(--gold) / 0.2);
      border-color: hsl(var(--gold));
    }

    .seat-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .seat-label {
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }

    .seat-role {
      font-size: 0.5625rem;
      opacity: 0.7;
    }

    .ai-type-select {
      width: 100%;
      max-width: 9rem;
      padding: 0.125rem 0.25rem;
      font-size: 0.625rem;
      background: hsl(var(--input));
      border: 1px solid hsl(var(--gold) / 0.4);
      border-radius: 0.25rem;
      color: hsl(var(--gold));
      outline: none;
      cursor: pointer;
      transition: border-color 0.15s ease;
    }

    .ai-type-select:focus {
      border-color: hsl(var(--gold));
    }

    .ai-type-select:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .you-marker {
      width: 3rem;
      height: 3rem;
      border-radius: 50%;
      background: hsl(var(--primary) / 0.15);
      border: 2px solid hsl(var(--primary));
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.625rem;
      font-weight: 700;
      color: hsl(var(--primary));
      text-transform: uppercase;
      letter-spacing: 0.04em;
      flex-shrink: 0;
    }

    .hint {
      margin: 0;
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .fill-bots-toggle {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: 100%;
      padding: 0.5rem 0.75rem;
      background: hsl(var(--muted) / 0.2);
      border: 1.5px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .fill-bots-toggle:hover:not(:disabled) {
      background: hsl(var(--muted) / 0.4);
      border-color: hsl(var(--muted-foreground));
    }

    .fill-bots-toggle.active {
      background: hsl(var(--gold) / 0.12);
      border-color: hsl(var(--gold) / 0.5);
      color: hsl(var(--gold));
    }

    .fill-bots-toggle.active:hover:not(:disabled) {
      background: hsl(var(--gold) / 0.2);
      border-color: hsl(var(--gold));
    }

    .fill-bots-toggle:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .error-msg {
      color: hsl(var(--destructive));
      font-size: 0.8125rem;
      margin: 0;
      text-align: center;
    }

    /* Actions */
    .actions {
      display: flex;
      gap: 0.5rem;
    }

    .cancel-btn {
      flex: 1;
      padding: 0.5rem;
      font-size: 0.875rem;
      font-weight: 500;
      background: transparent;
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .cancel-btn:hover:not(:disabled) {
      background: hsl(var(--muted) / 0.3);
      color: hsl(var(--foreground));
    }

    .cancel-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .submit-btn {
      flex: 2;
    }

    /* Timer presets */
    .timer-presets {
      display: flex;
      gap: 0.375rem;
    }

    .timer-btn {
      flex: 1;
      padding: 0.375rem 0.5rem;
      font-size: 0.75rem;
      font-weight: 600;
      background: hsl(var(--muted) / 0.2);
      border: 1.5px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .timer-btn:hover:not(:disabled) {
      background: hsl(var(--muted) / 0.4);
      border-color: hsl(var(--muted-foreground));
    }

    .timer-btn.active {
      background: hsl(var(--primary) / 0.15);
      border-color: hsl(var(--primary));
      color: hsl(var(--primary));
    }

    .timer-btn.active:hover:not(:disabled) {
      background: hsl(var(--primary) / 0.25);
    }

    .timer-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    /* Rated toggle — subtle secondary option */
    .rated-field {
      margin-top: -0.5rem;
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

    .rated-subtle:hover:not(:disabled) {
      color: hsl(var(--muted-foreground));
    }

    .rated-subtle.active {
      color: hsl(var(--muted-foreground) / 0.8);
    }

    .rated-subtle:disabled {
      opacity: 0.4;
      cursor: not-allowed;
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

    /* Invite only toggle */
    .invite-only-toggle {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: 100%;
      padding: 0.5rem 0.75rem;
      background: hsl(var(--muted) / 0.2);
      border: 1.5px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      font-size: 0.8125rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .invite-only-toggle:hover:not(:disabled) {
      background: hsl(var(--muted) / 0.4);
      border-color: hsl(var(--muted-foreground));
    }

    .invite-only-toggle.active {
      background: hsl(var(--gold) / 0.12);
      border-color: hsl(var(--gold) / 0.5);
      color: hsl(var(--gold));
    }

    .invite-only-toggle.active:hover:not(:disabled) {
      background: hsl(var(--gold) / 0.2);
      border-color: hsl(var(--gold));
    }

    .invite-only-toggle:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .toggle-track {
      margin-left: auto;
      width: 2rem;
      height: 1.125rem;
      border-radius: 9999px;
      background: hsl(var(--muted));
      position: relative;
      transition: background 0.15s ease;
    }

    .invite-only-toggle.active .toggle-track,
    .fill-bots-toggle.active .toggle-track {
      background: hsl(var(--gold));
    }

    .toggle-thumb {
      position: absolute;
      top: 0.125rem;
      left: 0.125rem;
      width: 0.875rem;
      height: 0.875rem;
      border-radius: 50%;
      background: hsl(var(--foreground));
      transition: transform 0.15s ease;
    }

    .invite-only-toggle.active .toggle-thumb,
    .fill-bots-toggle.active .toggle-thumb {
      transform: translateX(0.875rem);
      background: hsl(var(--background));
    }
  `],
})
export class CreateRoomFormComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly session = inject(ClientSessionService);
  private readonly auth = inject(AuthService);

  readonly BotIcon = Bot;
  readonly UserPlusIcon = UserPlus;
  readonly LockIcon = Lock;
  readonly TrophyIcon = Trophy;

  readonly displayName = () => this.auth.user()?.displayName ?? '';

  readonly roomCreated = output<RoomResponse>();
  readonly cancelled = output<void>();

  roomName = '';
  isRanked = true;
  inviteOnly = false;
  selectedTimer = 20;
  readonly timerPresets = [
    { label: '5s', value: 5 },
    { label: '10s', value: 10 },
    { label: '20s', value: 20 },
    { label: '1min', value: 60 },
  ];
  aiSeats: Record<'Left' | 'Top' | 'Right', string | null> = {
    Left: null,
    Top: null,
    Right: null,
  };
  readonly aiTypes = signal<AiTypeInfo[]>([{ name: DEFAULT_AI_TYPE, displayName: 'Deterministic', difficulty: 0, rating: 0, pun: null, description: null, author: null }]);
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string>('');

  ngOnInit(): void {
    this.api.getAiTypes().subscribe({
      next: (types) => {
        if (types.length > 0) {
          this.aiTypes.set(types);
        }
      },
    });
  }

  get allAi(): boolean {
    return !!this.aiSeats.Left && !!this.aiSeats.Top && !!this.aiSeats.Right;
  }

  toggleSeat(seat: 'Left' | 'Top' | 'Right'): void {
    if (this.aiSeats[seat]) {
      this.aiSeats[seat] = null;
    } else {
      this.aiSeats[seat] = DEFAULT_AI_TYPE;
    }
  }

  toggleAllAi(): void {
    const fill = !this.allAi;
    const type = fill ? DEFAULT_AI_TYPE : null;
    this.aiSeats.Left = type;
    this.aiSeats.Top = type;
    this.aiSeats.Right = type;
  }

  private getAiSeats(): AiSeat[] {
    const seats: AiSeat[] = [];
    if (this.aiSeats.Left) seats.push({ position: PlayerPosition.Left, aiType: this.aiSeats.Left });
    if (this.aiSeats.Top) seats.push({ position: PlayerPosition.Top, aiType: this.aiSeats.Top });
    if (this.aiSeats.Right) seats.push({ position: PlayerPosition.Right, aiType: this.aiSeats.Right });
    return seats;
  }

  onSubmit(): void {
    const name = this.roomName.trim() || null;

    this.submitting.set(true);
    this.error.set('');

    this.api.createRoom(name, this.getAiSeats(), this.selectedTimer, this.inviteOnly, this.isRanked).subscribe({
      next: (response) => {
        this.session.joinRoom(
          response.room.roomId,
          response.clientId,
          response.position
        );

        this.submitting.set(false);
        this.roomCreated.emit(response.room);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to create room');
        this.submitting.set(false);
      },
    });
  }
}
