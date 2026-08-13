import { Component, HostListener, output } from '@angular/core';
import { LucideAngularModule, X, Layers } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

/**
 * Explains how the deck is rebuilt between deals (it is never shuffled
 * during a match) and why the cut matters.
 */
@Component({
  selector: 'app-cut-info-popup',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop" (click)="closed.emit()"></div>
    <div class="popup-container" (click)="closed.emit()">
      <div class="popup-panel" (click)="$event.stopPropagation()">
        <button class="close-btn" (click)="closed.emit()">
          <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>

        <div class="header">
          <i-lucide [img]="LayersIcon" [size]="20" [strokeWidth]="2" class="header-icon"></i-lucide>
          <h2 class="title">{{ t('cutInfo.title') }}</h2>
        </div>

        <div class="body">
          <p>{{ t('cutInfo.intro') }}</p>
          <p>{{ t('cutInfo.collect') }}</p>
          <p>{{ t('cutInfo.cutOnly') }}</p>
          <p>{{ t('cutInfo.nudge') }}</p>
        </div>
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    :host {
      display: contents;
    }

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
      max-width: 400px;
      width: calc(100% - 2rem);
      animation: scaleIn 0.25s ease;
      display: flex;
      flex-direction: column;
      gap: 1rem;
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

    .header {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .header-icon {
      color: hsl(var(--gold));
      display: inline-flex;
    }

    .title {
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      margin: 0;
    }

    .body {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .body p {
      margin: 0;
      font-size: 0.875rem;
      line-height: 1.55;
      color: hsl(var(--muted-foreground));
    }

    .body p:first-child {
      color: hsl(var(--foreground));
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes scaleIn {
      from { opacity: 0; transform: scale(0.95); }
      to { opacity: 1; transform: scale(1); }
    }
  `],
})
export class CutInfoPopupComponent {
  readonly XIcon = X;
  readonly LayersIcon = Layers;

  readonly closed = output<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
