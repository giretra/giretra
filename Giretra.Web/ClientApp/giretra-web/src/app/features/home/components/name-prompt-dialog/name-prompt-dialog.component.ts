import { Component, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-name-prompt-dialog',
  standalone: true,
  imports: [FormsModule, HlmButton],
  template: `
    <div class="overlay">
      <div class="dialog">
        <!-- Decorative suits -->
        <div class="decor-suits">
          <span class="decor-suit spade">\u2660</span>
          <span class="decor-suit heart">\u2665</span>
          <span class="decor-suit diamond">\u2666</span>
          <span class="decor-suit club">\u2663</span>
        </div>

        <div class="dialog-header">
          <h2>Welcome to <span class="brand-name">Giretra</span></h2>
          <p>Choose a name to sit at the table</p>
        </div>

        <form (ngSubmit)="onSubmit()">
          <div class="input-group">
            <input
              type="text"
              class="name-input"
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
            class="submit-btn"
            [disabled]="!name.trim()"
          >
            Take a seat
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 1000;
      animation: fadeIn 0.2s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .dialog {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 2rem 2rem 1.5rem;
      width: 100%;
      max-width: 380px;
      animation: slideUp 0.25s ease;
      position: relative;
      overflow: hidden;
    }

    @keyframes slideUp {
      from {
        opacity: 0;
        transform: translateY(16px) scale(0.98);
      }
      to {
        opacity: 1;
        transform: translateY(0) scale(1);
      }
    }

    /* Decorative suits */
    .decor-suits {
      display: flex;
      justify-content: center;
      gap: 0.75rem;
      margin-bottom: 1.25rem;
    }

    .decor-suit {
      font-size: 1.25rem;
      opacity: 0.25;
    }

    .decor-suit.heart, .decor-suit.diamond {
      color: hsl(0 65% 55%);
    }

    .decor-suit.spade, .decor-suit.club {
      color: hsl(var(--foreground));
    }

    .dialog-header {
      text-align: center;
      margin-bottom: 1.5rem;
    }

    .dialog-header h2 {
      margin: 0 0 0.375rem 0;
      font-size: 1.375rem;
      font-weight: 700;
      color: hsl(var(--foreground));
    }

    .brand-name {
      color: hsl(var(--primary));
    }

    .dialog-header p {
      margin: 0;
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .input-group {
      margin-bottom: 1rem;
    }

    .name-input {
      width: 100%;
      padding: 0.75rem 1rem;
      font-size: 1rem;
      font-weight: 500;
      background: hsl(var(--input));
      border: 1.5px solid hsl(var(--border));
      border-radius: 0.625rem;
      color: hsl(var(--foreground));
      outline: none;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
      text-align: center;
    }

    .name-input:focus {
      border-color: hsl(var(--primary));
      box-shadow: 0 0 0 3px hsl(var(--primary) / 0.15);
    }

    .name-input::placeholder {
      color: hsl(var(--muted-foreground));
      font-weight: 400;
    }

    .submit-btn {
      width: 100%;
      padding: 0.75rem;
      font-weight: 600;
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
