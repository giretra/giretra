import { Component, input, output, computed } from '@angular/core';
import { GameMode } from '../../../../../api/generated/signalr-types.generated';
import { ValidAction } from '../../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { SuitIconComponent } from '../../../../../shared/components/suit-icon/suit-icon.component';
import { CardSuit } from '../../../../../api/generated/signalr-types.generated';
import { LucideAngularModule, Check, ChevronsUp } from 'lucide-angular';

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
  imports: [HlmButton, SuitIconComponent, LucideAngularModule],
  template: `
    <div class="bid-buttons">
      <!-- Announce section -->
      @if (suitButtons().length > 0) {
        <div class="section-label">Announce</div>
        <div class="button-row suits">
          @for (btn of suitButtons(); track btn.label) {
            <button
              hlmBtn
              [variant]="btn.variant"
              class="bid-btn suit-btn"
              (click)="selectAction(btn)"
            >
              @if (btn.suit) {
                <app-suit-icon [suit]="btn.suit" size="1.5rem" />
              } @else {
                {{ btn.label }}
              }
            </button>
          }
        </div>
      }

      <!-- Respond section -->
      @if (actionButtons().length > 0) {
        <div class="section-label">Respond</div>
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
                Accept
              } @else if (btn.actionType === 'Double') {
                <i-lucide [img]="ChevronsUpIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>Double <span class="multiplier">×2</span></span>
                @if (getModeHintSuit(btn.mode); as suit) {
                  <app-suit-icon [suit]="suit" size="1rem" />
                } @else if (getModeHintText(btn.mode); as text) {
                  <span class="mode-hint">{{ text }}</span>
                }
              } @else if (btn.actionType === 'Redouble') {
                <i-lucide [img]="ChevronsUpIcon" [size]="16" [strokeWidth]="2"></i-lucide>
                <span>Redouble <span class="multiplier">×4</span></span>
                @if (getModeHintSuit(btn.mode); as suit) {
                  <app-suit-icon [suit]="suit" size="1rem" />
                } @else if (getModeHintText(btn.mode); as text) {
                  <span class="mode-hint">{{ text }}</span>
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
      background: hsl(142, 50%, 35%);
      color: white;
      border-color: hsl(142, 50%, 40%);
    }

    .accept-btn:hover {
      background: hsl(142, 50%, 40%);
    }

    .double-btn {
      background: hsl(0, 72%, 45%);
      color: white;
      border-color: hsl(0, 72%, 50%);
    }

    .double-btn:hover {
      background: hsl(0, 72%, 50%);
    }

    .multiplier {
      font-weight: 800;
      opacity: 0.7;
      font-size: 0.75em;
    }

    .mode-hint {
      font-size: 0.6875rem;
      opacity: 0.8;
      font-weight: 600;
      padding-left: 0.125rem;
    }
  `],
})
export class BidButtonRowComponent {
  readonly CheckIcon = Check;
  readonly ChevronsUpIcon = ChevronsUp;

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

  getModeHintSuit(mode: GameMode | null): CardSuit | null {
    return mode ? this.modeToSuit[mode] ?? null : null;
  }

  getModeHintText(mode: GameMode | null): string | null {
    if (mode === GameMode.SansAs) return 'Sans As';
    if (mode === GameMode.ToutAs) return 'Tout As';
    return null;
  }

  selectAction(btn: BidButton): void {
    this.actionSelected.emit({
      actionType: btn.actionType,
      mode: btn.mode,
    });
  }
}
