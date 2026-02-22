import { Component, input, OnDestroy, signal, effect } from '@angular/core';

@Component({
  selector: 'app-turn-timer',
  standalone: true,
  template: `
    <div
      class="timer-pill"
      [class.warning]="remaining() <= 30 && remaining() > 10"
      [class.critical]="remaining() <= 10"
    >
      {{ remaining() }}s
    </div>
  `,
  styles: [`
    .timer-pill {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 2.25rem;
      height: 1.25rem;
      padding: 0 0.375rem;
      font-size: 0.625rem;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      color: hsl(var(--primary-foreground));
      background: hsl(var(--primary));
      border-radius: 9999px;
      transition: background 0.15s ease;
    }

    .timer-pill.warning {
      background: hsl(var(--gold));
      color: hsl(220, 20%, 10%);
    }

    .timer-pill.critical {
      background: hsl(var(--destructive));
      color: hsl(var(--destructive-foreground));
      animation: pulse 1s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.6; }
    }
  `],
})
export class TurnTimerComponent implements OnDestroy {
  readonly deadline = input<Date | null>(null);
  readonly remaining = signal<number>(0);

  private intervalId: ReturnType<typeof setInterval> | null = null;

  constructor() {
    effect(() => {
      const d = this.deadline();
      this.clearInterval();
      if (d) {
        this.updateRemaining(d);
        this.intervalId = setInterval(() => this.updateRemaining(d), 1000);
      } else {
        this.remaining.set(0);
      }
    });
  }

  ngOnDestroy(): void {
    this.clearInterval();
  }

  private updateRemaining(deadline: Date): void {
    const diff = Math.max(0, Math.ceil((deadline.getTime() - Date.now()) / 1000));
    this.remaining.set(diff);
  }

  private clearInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }
}
