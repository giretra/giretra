import { Component, input, output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, RoomResponse } from '../../../../core/services/api.service';
import { ClientSessionService } from '../../../../core/services/client-session.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { LucideAngularModule, Bot, UserPlus } from 'lucide-angular';

@Component({
  selector: 'app-create-room-form',
  standalone: true,
  imports: [FormsModule, HlmButton, LucideAngularModule],
  template: `
    <div class="create-form">
      <div class="form-header">
        <h3>New Table</h3>
        <button
          type="button"
          class="close-btn"
          (click)="cancelled.emit()"
        >&times;</button>
      </div>

      <form (ngSubmit)="onSubmit()">
        <!-- Room name -->
        <div class="field">
          <label for="roomName" class="field-label">Table Name</label>
          <input
            id="roomName"
            type="text"
            class="text-input"
            [(ngModel)]="roomName"
            name="roomName"
            [placeholder]="playerName() + '\u2019s table'"
            maxlength="50"
            [disabled]="submitting()"
          />
        </div>

        <!-- AI seats -->
        <div class="field">
          <div class="ai-header">
            <label class="field-label">AI Players</label>
            <button
              type="button"
              class="toggle-all-btn"
              [disabled]="submitting()"
              (click)="toggleAllAi()"
            >
              {{ allAi ? 'Clear all' : 'Fill all' }}
            </button>
          </div>

          <!-- Visual compass seat selector -->
          <div class="seat-selector">
            <!-- Top (Partner) -->
            <div class="seat-row">
              <button
                type="button"
                class="seat-btn"
                [class.active]="aiTop"
                [disabled]="submitting()"
                (click)="aiTop = !aiTop"
              >
                @if (aiTop) {
                  <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                } @else {
                  <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                }
                <span class="seat-label">Top</span>
                <span class="seat-role">Partner</span>
              </button>
            </div>
            <!-- Middle: Left + You + Right -->
            <div class="seat-row middle">
              <button
                type="button"
                class="seat-btn"
                [class.active]="aiLeft"
                [disabled]="submitting()"
                (click)="aiLeft = !aiLeft"
              >
                @if (aiLeft) {
                  <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                } @else {
                  <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                }
                <span class="seat-label">Left</span>
              </button>
              <div class="you-marker">You</div>
              <button
                type="button"
                class="seat-btn"
                [class.active]="aiRight"
                [disabled]="submitting()"
                (click)="aiRight = !aiRight"
              >
                @if (aiRight) {
                  <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                } @else {
                  <i-lucide [img]="UserPlusIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                }
                <span class="seat-label">Right</span>
              </button>
            </div>
          </div>

          <p class="hint">Click seats to toggle AI opponents</p>
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
            Cancel
          </button>
          <button
            type="submit"
            hlmBtn
            variant="default"
            class="submit-btn"
            [disabled]="submitting()"
          >
            @if (submitting()) {
              Creating...
            } @else {
              Create Table
            }
          </button>
        </div>
      </form>
    </div>
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

    /* AI header */
    .ai-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .toggle-all-btn {
      padding: 0.1875rem 0.5rem;
      font-size: 0.6875rem;
      font-weight: 600;
      color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.1);
      border: 1px solid hsl(var(--primary) / 0.25);
      border-radius: 9999px;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .toggle-all-btn:hover:not(:disabled) {
      background: hsl(var(--primary) / 0.2);
      border-color: hsl(var(--primary) / 0.4);
    }

    .toggle-all-btn:disabled {
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
  `],
})
export class CreateRoomFormComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(ClientSessionService);

  readonly BotIcon = Bot;
  readonly UserPlusIcon = UserPlus;

  readonly playerName = input.required<string>();

  readonly roomCreated = output<RoomResponse>();
  readonly cancelled = output<void>();

  roomName = '';
  aiLeft = false;
  aiTop = false;
  aiRight = false;
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string>('');

  get allAi(): boolean {
    return this.aiLeft && this.aiTop && this.aiRight;
  }

  toggleAllAi(): void {
    const fill = !this.allAi;
    this.aiLeft = fill;
    this.aiTop = fill;
    this.aiRight = fill;
  }

  private getAiPositions(): PlayerPosition[] {
    const positions: PlayerPosition[] = [];
    if (this.aiLeft) positions.push(PlayerPosition.Left);
    if (this.aiTop) positions.push(PlayerPosition.Top);
    if (this.aiRight) positions.push(PlayerPosition.Right);
    return positions;
  }

  onSubmit(): void {
    const name = this.roomName.trim() || null;

    this.submitting.set(true);
    this.error.set('');

    this.api.createRoom(name, this.playerName(), this.getAiPositions()).subscribe({
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
