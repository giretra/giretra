import { Component, input, output, computed, inject } from '@angular/core';
import { GameMode } from '../../../../../api/generated/signalr-types.generated';
import { ValidAction } from '../../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { GameModeIconComponent } from '../../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { LucideAngularModule, Check, ChevronsUp } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

interface BidButton {
  label: string;
  actionType: string;
  mode: GameMode | null;
  variant: 'default' | 'secondary' | 'destructive';
}

@Component({
  selector: 'app-bid-button-row',
  standalone: true,
  imports: [HlmButton, GameModeIconComponent, LucideAngularModule, TranslocoDirective],
  template: `
    <div class="bid-buttons" *transloco="let t">
      <!-- Announce section -->
      @if (suitButtons().length > 0) {
        <div class="section-label">{{ t('negotiation.announce') }}</div>
        <div class="button-row suits">
          @for (btn of suitButtons(); track btn.label) {
            <button
              hlmBtn
              [variant]="btn.variant"
              class="bid-btn suit-btn"
              [title]="btn.label"
              (click)="selectAction(btn)"
            >
              <app-game-mode-icon [mode]="btn.mode!" size="1.5rem" />
            </button>
          }
        </div>
      }

      <!-- Respond section -->
      @if (actionButtons().length > 0) {
        <div class="section-label">{{ t('negotiation.respond') }}</div>
        <div class="button-row actions">
          @for (btn of actionButtons(); track btn.label) {
            <button
              hlmBtn
              [variant]="btn.variant"
              class="bid-btn"
              [class.accept-btn]="btn.actionType === 'Accept'"
              [class.double-btn]="btn.actionType === 'Double' || btn.actionType === 'Redouble'"
              (click)="selectAction(btn)"
            >
              @if (btn.actionType === 'Accept') {
                <i-lucide [img]="CheckIcon" [size]="16" [strokeWidth]="2.5"></i-lucide>
                {{ t('negotiation.accept') }}
              } @else if (btn.actionType === 'Double') {
                <i-lucide [img]="ChevronsUpIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>{{ t('negotiation.double') }} <span class="multiplier">\u00d72</span></span>
                @if (btn.mode) {
                  <app-game-mode-icon [mode]="btn.mode" size="1rem" />
                }
              } @else if (btn.actionType === 'Redouble') {
                <i-lucide [img]="ChevronsUpIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>{{ t('negotiation.redouble') }} <span class="multiplier">\u00d74</span></span>
                @if (btn.mode) {
                  <app-game-mode-icon [mode]="btn.mode" size="1rem" />
                }
              } @else {
                {{ btn.label }}
              }
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .bid-buttons {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      padding: 0.25rem;
    }

    .section-label {
      font-size: 0.625rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .button-row {
      display: flex;
      justify-content: center;
      gap: 0.375rem;
      flex-wrap: wrap;
    }

    .bid-btn {
      min-width: 3rem;
      padding: 0.5rem 0.75rem;
      cursor: pointer;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3), 0 1px 2px rgba(0, 0, 0, 0.2);
      border: 1px solid rgba(255, 255, 255, 0.1);
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
    }

    .bid-btn:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 8px rgba(0, 0, 0, 0.4), 0 2px 4px rgba(0, 0, 0, 0.3);
      filter: brightness(1.15);
    }

    .bid-btn:active {
      transform: translateY(0);
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
    }

    .suit-btn {
      width: 3rem;
      height: 3rem;
      min-width: unset;
      padding: 0;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .accept-btn {
      background: transparent;
      color: hsl(142, 50%, 50%);
      border-color: hsl(142, 50%, 40%);
    }

    .accept-btn:hover {
      background: hsl(142, 50%, 40% / 0.15);
    }

    .double-btn {
      background: transparent;
      color: hsl(0, 72%, 60%);
      border-color: hsl(0, 72%, 50%);
    }

    .double-btn:hover {
      background: hsl(0, 72%, 50% / 0.15);
    }

    .multiplier {
      font-weight: 800;
      opacity: 0.7;
      font-size: 0.75em;
    }
  `],
})
export class BidButtonRowComponent {
  readonly CheckIcon = Check;
  readonly ChevronsUpIcon = ChevronsUp;
  private readonly transloco = inject(TranslocoService);

  readonly validActions = input<ValidAction[]>([]);

  readonly actionSelected = output<{ actionType: string; mode?: string | null }>();

  private static readonly modeTranslationKeys: Record<string, string> = {
    [GameMode.ColourClubs]: 'game.modes.clubs',
    [GameMode.ColourDiamonds]: 'game.modes.diamonds',
    [GameMode.ColourHearts]: 'game.modes.hearts',
    [GameMode.ColourSpades]: 'game.modes.spades',
    [GameMode.NoTrumps]: 'game.modes.noTrumps',
    [GameMode.AllTrumps]: 'game.modes.allTrumps',
  };

  readonly suitButtons = computed<BidButton[]>(() => {
    const actions = this.validActions();
    return actions
      .filter((a) => a.actionType === 'Announce' && a.mode)
      .map((a) => ({
        label: this.transloco.translate(
          BidButtonRowComponent.modeTranslationKeys[a.mode!] ?? a.mode!,
        ),
        actionType: a.actionType,
        mode: a.mode,
        variant: 'secondary' as const,
      }));
  });

  readonly actionButtons = computed<BidButton[]>(() => {
    const actions = this.validActions();
    const buttons: BidButton[] = [];

    // Accept button
    if (actions.some((a) => a.actionType === 'Accept')) {
      buttons.push({
        label: 'Accept',
        actionType: 'Accept',
        mode: null,
        variant: 'default',
      });
    }

    // Double button
    const doubleAction = actions.find((a) => a.actionType === 'Double');
    if (doubleAction) {
      buttons.push({
        label: '\u00d72',
        actionType: 'Double',
        mode: doubleAction.mode,
        variant: 'destructive',
      });
    }

    // Redouble button
    const redoubleAction = actions.find((a) => a.actionType === 'Redouble');
    if (redoubleAction) {
      buttons.push({
        label: '\u00d74',
        actionType: 'Redouble',
        mode: redoubleAction.mode,
        variant: 'destructive',
      });
    }

    return buttons;
  });

  selectAction(btn: BidButton): void {
    this.actionSelected.emit({
      actionType: btn.actionType,
      mode: btn.mode,
    });
  }
}
