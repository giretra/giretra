import { Component, input, output, computed } from '@angular/core';
import { GameMode } from '../../../../../api/generated/signalr-types.generated';
import { ValidAction } from '../../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { SuitIconComponent } from '../../../../../shared/components/suit-icon/suit-icon.component';
import { CardSuit } from '../../../../../api/generated/signalr-types.generated';

interface BidButton {
  label: string;
  actionType: string;
  mode: GameMode | null;
  variant: 'default' | 'secondary' | 'destructive';
  suit?: CardSuit;
}

@Component({
  selector: 'app-bid-button-row',
  standalone: true,
  imports: [HlmButton, SuitIconComponent],
  template: `
    <div class="bid-buttons">
      <!-- Suit buttons row -->
      <div class="button-row suits">
        @for (btn of suitButtons(); track btn.label) {
          <button
            hlmBtn
            [variant]="btn.variant"
            class="bid-btn suit-btn"
            (click)="selectAction(btn)"
          >
            @if (btn.suit) {
              <app-suit-icon [suit]="btn.suit" size="1.25rem" />
            } @else {
              {{ btn.label }}
            }
          </button>
        }
      </div>

      <!-- Action buttons row -->
      <div class="button-row actions">
        @for (btn of actionButtons(); track btn.label) {
          <button
            hlmBtn
            [variant]="btn.variant"
            class="bid-btn"
            (click)="selectAction(btn)"
          >
            {{ btn.label }}
          </button>
        }
      </div>
    </div>
  `,
  styles: [`
    .bid-buttons {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 0.25rem;
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
    }

    .suit-btn {
      min-width: 2.5rem;
    }
  `],
})
export class BidButtonRowComponent {
  readonly validActions = input<ValidAction[]>([]);

  readonly actionSelected = output<{ actionType: string; mode?: string | null }>();

  // Map game modes to suits
  private readonly modeToSuit: Record<string, CardSuit> = {
    [GameMode.ColourClubs]: CardSuit.Clubs,
    [GameMode.ColourDiamonds]: CardSuit.Diamonds,
    [GameMode.ColourHearts]: CardSuit.Hearts,
    [GameMode.ColourSpades]: CardSuit.Spades,
  };

  readonly suitButtons = computed<BidButton[]>(() => {
    const actions = this.validActions();
    const buttons: BidButton[] = [];

    // Suit announce buttons
    for (const action of actions) {
      if (action.actionType === 'Announce' && action.mode) {
        const suit = this.modeToSuit[action.mode];
        if (suit) {
          buttons.push({
            label: action.mode,
            actionType: action.actionType,
            mode: action.mode,
            variant: 'secondary',
            suit,
          });
        }
      }
    }

    // Sans As button
    const sansAs = actions.find(
      (a) => a.actionType === 'Announce' && a.mode === GameMode.SansAs
    );
    if (sansAs) {
      buttons.push({
        label: 'Sans As',
        actionType: 'Announce',
        mode: GameMode.SansAs,
        variant: 'secondary',
      });
    }

    // Tout As button
    const toutAs = actions.find(
      (a) => a.actionType === 'Announce' && a.mode === GameMode.ToutAs
    );
    if (toutAs) {
      buttons.push({
        label: 'Tout As',
        actionType: 'Announce',
        mode: GameMode.ToutAs,
        variant: 'secondary',
      });
    }

    return buttons;
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
