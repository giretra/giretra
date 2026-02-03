import { Component, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-name-prompt-dialog',
  standalone: true,
  imports: [FormsModule, HlmButton],
  template: `
    <div class="overlay">
      <div class="dialog">
        <div class="dialog-header">
          <h2>Welcome to Giretra</h2>
          <p>Enter your name to get started</p>
        </div>

        <form (ngSubmit)="onSubmit()">
          <div class="form-field">
            <input
              type="text"
              class="input"
              [(ngModel)]="name"
              name="name"
              placeholder="Your name"
              maxlength="30"
              required
              autofocus
            />
          </div>

          <button
            type="submit"
            hlmBtn
            variant="default"
            class="submit-button"
            [disabled]="!name.trim()"
          >
            Continue
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 1000;
    }

    .dialog {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      padding: 2rem;
      width: 100%;
      max-width: 400px;
      animation: slideUp 0.2s ease;
    }

    @keyframes slideUp {
      from {
        opacity: 0;
        transform: translateY(10px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .dialog-header {
      text-align: center;
      margin-bottom: 1.5rem;
    }

    .dialog-header h2 {
      margin: 0 0 0.5rem 0;
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--primary));
    }

    .dialog-header p {
      margin: 0;
      color: hsl(var(--muted-foreground));
    }

    .form-field {
      margin-bottom: 1rem;
    }

    .input {
      width: 100%;
      padding: 0.75rem 1rem;
      font-size: 1rem;
      background: hsl(var(--input));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--foreground));
      outline: none;
      transition: border-color 0.15s ease;
      text-align: center;
    }

    .input:focus {
      border-color: hsl(var(--primary));
    }

    .input::placeholder {
      color: hsl(var(--muted-foreground));
    }

    .submit-button {
      width: 100%;
      padding: 0.75rem;
    }
  `],
})
export class NamePromptDialogComponent {
  readonly nameSubmitted = output<string>();

  name = '';

  onSubmit(): void {
    const trimmedName = this.name.trim();
    if (trimmedName) {
      this.nameSubmitted.emit(trimmedName);
    }
  }
}
