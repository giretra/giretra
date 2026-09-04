import { Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { AiTypeInfo } from '../../../../core/services/api.service';

const DEFAULT_AI_TYPE = 'DeterministicPlayer';

@Component({
  selector: 'app-quick-game-dialog',
  standalone: true,
  imports: [FormsModule, TranslocoDirective, DialogModule, ButtonModule, AvatarModule, TagModule, ToggleSwitchModule],
  template: `
    <ng-container *transloco="let t">
      <p-dialog
        [visible]="open()"
        (visibleChange)="onVisibleChange($event)"
        [modal]="true"
        [draggable]="false"
        [dismissableMask]="true"
        [style]="{ width: '30rem' }"
        [breakpoints]="{ '640px': '95vw' }"
        [header]="t('quickGame.title')"
        appendTo="body"
      >
        <p class="intro">{{ t('quickGame.chooseDifficulty') }}</p>

        <div class="bot-list">
          @for (bot of sortedAiTypes(); track bot.name) {
            <button
              type="button"
              class="bot-row"
              [class.selected]="selectedBot() === bot.name"
              (click)="selectedBot.set(bot.name)"
            >
              <p-avatar [label]="bot.displayName.charAt(0)" shape="circle" styleClass="bot-avatar" />
              <span class="bot-info">
                <span class="bot-name">{{ bot.displayName }}</span>
                @if (bot.pun) {
                  <span class="bot-pun">{{ bot.pun }}</span>
                }
              </span>
              <span class="bot-side">
                <span class="dots" [title]="t('quickGame.chooseDifficulty')">
                  @for (dot of difficultyDots(bot.difficulty); track $index) {
                    <span class="dot" [class.filled]="dot"></span>
                  }
                </span>
                <p-tag [value]="bot.rating.toString()" severity="secondary" [rounded]="true" />
              </span>
              <i class="pi bot-check" [class.pi-check-circle]="selectedBot() === bot.name" [class.pi-circle]="selectedBot() !== bot.name"></i>
            </button>
          }
        </div>

        <div class="setting-row">
          <div>
            <div class="setting-label"><i class="pi pi-trophy"></i>{{ t('quickGame.rated') }}</div>
            <div class="setting-hint">{{ t('createForm.ratedHint') }}</div>
          </div>
          <p-toggleswitch [ngModel]="isRanked()" (ngModelChange)="isRanked.set($event)" />
        </div>

        <div class="meta-row">
          <span><i class="pi pi-clock"></i>{{ t('quickGame.turnTimer') }}</span>
          <a href="https://www.giretra.com/build-your-bot/" target="_blank" rel="noopener">
            {{ t('quickGame.buildYourBot') }} <i class="pi pi-external-link"></i>
          </a>
        </div>

        <ng-template #footer>
          <p-button [label]="t('quickGame.createRoomInstead')" severity="secondary" [text]="true" (onClick)="createRoom.emit()" />
          <p-button [label]="t('quickGame.play')" icon="pi pi-play" [disabled]="!selectedBot()" (onClick)="onPlay()" />
        </ng-template>
      </p-dialog>
    </ng-container>
  `,
  styles: [`
    .intro { margin:0 0 0.75rem; font-size:0.875rem; color:var(--text-color-secondary); }
    .bot-list { display:flex; flex-direction:column; gap:0.375rem; max-height:20rem; overflow-y:auto; margin-bottom:0.75rem; }
    .bot-row { display:flex; align-items:center; gap:0.75rem; width:100%; padding:0.625rem 0.75rem; border:1px solid transparent; border-radius:0.875rem; background:transparent; color:inherit; text-align:left; cursor:pointer; transition:background-color var(--transition-duration), border-color var(--transition-duration); }
    .bot-row:hover { background:var(--surface-hover); }
    .bot-row.selected { background:color-mix(in srgb, var(--p-primary-color) 12%, transparent); border-color:color-mix(in srgb, var(--p-primary-color) 45%, transparent); }
    .bot-info { display:flex; flex-direction:column; gap:0.125rem; flex:1; min-width:0; }
    .bot-name { font-weight:500; }
    .bot-pun { font-size:0.75rem; color:var(--text-color-secondary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .bot-side { display:flex; align-items:center; gap:0.625rem; flex-shrink:0; }
    .dots { display:flex; gap:0.2rem; }
    .dot { width:0.375rem; height:0.375rem; border-radius:50%; background:var(--p-surface-700); }
    .dot.filled { background:var(--p-primary-400); }
    .bot-check { color:var(--text-color-secondary); font-size:1rem; }
    .selected .bot-check { color:var(--p-primary-400); }
    .meta-row { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:0.5rem; padding-top:0.5rem; font-size:0.8125rem; color:var(--text-color-secondary); }
    .meta-row i { margin-right:0.375rem; font-size:0.75rem; }
    .meta-row a { color:var(--p-primary-400); }
    .meta-row a i { margin:0 0 0 0.25rem; font-size:0.6875rem; }
  `],
})
export class QuickGameDialogComponent {
  readonly open = input<boolean>(false);
  readonly aiTypes = input<AiTypeInfo[]>([]);

  readonly play = output<{ aiType: string; isRanked: boolean }>();
  readonly closed = output<void>();
  readonly createRoom = output<void>();

  readonly sortedAiTypes = computed(() =>
    [...this.aiTypes()].sort((a, b) => b.rating - a.rating)
  );

  readonly selectedBot = signal<string>('');
  readonly isRanked = signal(true);

  constructor() {
    effect(() => {
      const types = this.sortedAiTypes();
      if (types.length > 0 && !this.selectedBot()) {
        const preferred = types.find((bot) => bot.name === DEFAULT_AI_TYPE);
        this.selectedBot.set((preferred ?? types[0]).name);
      }
    });
  }

  difficultyDots(difficulty: number): boolean[] {
    const max = 3;
    return Array.from({ length: max }, (_, i) => i < difficulty);
  }

  onVisibleChange(visible: boolean): void {
    if (!visible) {
      this.closed.emit();
    }
  }

  onPlay(): void {
    const bot = this.selectedBot();
    if (bot) {
      this.play.emit({ aiType: bot, isRanked: this.isRanked() });
    }
  }
}
