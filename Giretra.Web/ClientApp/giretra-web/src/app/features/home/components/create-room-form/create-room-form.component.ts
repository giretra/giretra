import { Component, input, output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService, RoomResponse } from '../../../../core/services/api.service';
import { ClientSessionService } from '../../../../core/services/client-session.service';
import { HlmButton } from '@spartan-ng/helm/button';

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

      <div class="form-field checkbox-field">
        <label class="checkbox-label">
          <input
            type="checkbox"
            [(ngModel)]="fillWithAi"
            name="fillWithAi"
            [disabled]="submitting()"
          />
          <span>Fill other seats with AI</span>
        </label>
        <p class="hint">Start playing immediately against 3 AI opponents</p>
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
  `],
})
export class CreateRoomFormComponent {
  private readonly api = inject(ApiService);
  private readonly session = inject(ClientSessionService);

  readonly playerName = input.required<string>();

  readonly roomCreated = output<RoomResponse>();
  readonly cancelled = output<void>();

  roomName = '';
  fillWithAi = false;
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string>('');

  onSubmit(): void {
    const name = this.roomName.trim() || null;

    this.submitting.set(true);
    this.error.set('');

    this.api.createRoom(name, this.playerName(), this.fillWithAi).subscribe({
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
