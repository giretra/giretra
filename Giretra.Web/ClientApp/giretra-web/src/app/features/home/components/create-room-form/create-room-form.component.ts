import { Component, input, output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, RoomResponse } from '../../../../core/services/api.service';
import { ClientSessionService } from '../../../../core/services/client-session.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';

@Component({
  selector: 'app-create-room-form',
  standalone: true,
  imports: [FormsModule, HlmButton],
  template: `
    <form class="create-form" (ngSubmit)="onSubmit()">
      <div class="form-header">
        <h3>Create a Room</h3>
        <button
          type="button"
          hlmBtn
          variant="ghost"
          size="sm"
          (click)="cancelled.emit()"
        >
          Cancel
        </button>
      </div>

      <div class="form-field">
        <label for="roomName">Room Name <span class="optional">(optional)</span></label>
        <input
          id="roomName"
          type="text"
          class="input"
          [(ngModel)]="roomName"
          name="roomName"
          [placeholder]="playerName() + '_#00001'"
          maxlength="50"
          [disabled]="submitting()"
        />
      </div>

      <div class="form-field ai-positions-field">
        <label>Fill seats with AI <span class="optional">(optional)</span></label>
        <div class="ai-positions-grid">
          <label class="checkbox-label">
            <input
              type="checkbox"
              [(ngModel)]="aiLeft"
              name="aiLeft"
              [disabled]="submitting()"
            />
            <span>Left</span>
          </label>
          <label class="checkbox-label">
            <input
              type="checkbox"
              [(ngModel)]="aiTop"
              name="aiTop"
              [disabled]="submitting()"
            />
            <span>Top (Partner)</span>
          </label>
          <label class="checkbox-label">
            <input
              type="checkbox"
              [(ngModel)]="aiRight"
              name="aiRight"
              [disabled]="submitting()"
            />
            <span>Right</span>
          </label>
        </div>
        <p class="hint">Select which positions should be AI opponents</p>
      </div>

      @if (error()) {
        <p class="error">{{ error() }}</p>
      }

      <button
        type="submit"
        hlmBtn
        variant="default"
        class="submit-button"
        [disabled]="submitting()"
      >
        @if (submitting()) {
          Creating...
        } @else {
          Create Room
        }
      </button>
    </form>
  `,
  styles: [`
    .create-form {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .form-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .form-header h3 {
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .form-field label {
      font-size: 0.875rem;
      font-weight: 500;
      color: hsl(var(--foreground));
    }

    .optional {
      font-weight: 400;
      color: hsl(var(--muted-foreground));
    }

    .input {
      width: 100%;
      padding: 0.5rem 0.75rem;
      font-size: 1rem;
      background: hsl(var(--input));
      border: 1px solid hsl(var(--border));
      border-radius: 0.375rem;
      color: hsl(var(--foreground));
      outline: none;
      transition: border-color 0.15s ease;
    }

    .input:focus {
      border-color: hsl(var(--primary));
    }

    .input::placeholder {
      color: hsl(var(--muted-foreground));
    }

    .input:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .error {
      color: hsl(var(--destructive));
      font-size: 0.875rem;
      margin: 0;
    }

    .submit-button {
      width: 100%;
    }

    .checkbox-field {
      gap: 0.25rem;
    }

    .checkbox-label {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      cursor: pointer;
    }

    .checkbox-label input[type="checkbox"] {
      width: 1rem;
      height: 1rem;
      accent-color: hsl(var(--primary));
      cursor: pointer;
    }

    .checkbox-label input[type="checkbox"]:disabled {
      cursor: not-allowed;
    }

    .hint {
      margin: 0;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .ai-positions-field {
      gap: 0.5rem;
    }

    .ai-positions-grid {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }

    .ai-positions-grid .checkbox-label {
      flex: 1;
      min-width: 80px;
      padding: 0.5rem 0.75rem;
      background: hsl(var(--muted) / 0.3);
      border: 1px solid hsl(var(--border));
      border-radius: 0.375rem;
      transition: background-color 0.15s ease, border-color 0.15s ease;
    }

    .ai-positions-grid .checkbox-label:has(input:checked) {
      background: hsl(var(--primary) / 0.1);
      border-color: hsl(var(--primary));
    }

    .ai-positions-grid .checkbox-label:hover:not(:has(input:disabled)) {
      background: hsl(var(--muted) / 0.5);
    }
  `],
})
export class CreateRoomFormComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(ClientSessionService);

  readonly playerName = input.required<string>();

  readonly roomCreated = output<RoomResponse>();
  readonly cancelled = output<void>();

  roomName = '';
  aiLeft = false;
  aiTop = false;
  aiRight = false;
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string>('');

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
        console.log('[CreateRoom] Response received:', {
          roomId: response.room.roomId,
          clientId: response.clientId,
          position: response.position,
          fullResponse: response,
        });

        // Store session info
        this.session.joinRoom(
          response.room.roomId,
          response.clientId,
          response.position
        );

        console.log('[CreateRoom] Session updated:', {
          storedClientId: this.session.clientId(),
          storedRoomId: this.session.roomId(),
        });

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
