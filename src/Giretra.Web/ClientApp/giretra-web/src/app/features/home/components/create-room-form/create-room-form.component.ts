import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageModule } from 'primeng/message';
import { AiSeat, AiTypeInfo, ApiService, RoomResponse } from '../../../../core/services/api.service';
import { ClientSessionService } from '../../../../core/services/client-session.service';
import { AuthService } from '../../../../core/services/auth.service';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';

const DEFAULT_AI_TYPE = 'DeterministicPlayer';

type SeatKey = 'Left' | 'Top' | 'Right';

interface SeatDef {
  key: SeatKey;
  area: string;
  labelKey: string;
}

@Component({
  selector: 'app-create-room-form',
  standalone: true,
  imports: [
    FormsModule,
    TranslocoDirective,
    DialogModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    ToggleSwitchModule,
    MessageModule,
  ],
  template: `
    <ng-container *transloco="let t">
      <p-dialog
        [visible]="open()"
        (visibleChange)="onVisibleChange($event)"
        [modal]="true"
        [draggable]="false"
        [style]="{ width: '34rem' }"
        [breakpoints]="{ '640px': '95vw' }"
        [header]="t('createForm.title')"
        appendTo="body"
      >
        <form id="createRoomForm" class="form" (ngSubmit)="onSubmit()">
          <!-- Room name -->
          <div class="field">
            <label for="roomName">{{ t('createForm.tableName') }}</label>
            <input
              pInputText
              id="roomName"
              name="roomName"
              type="text"
              fluid
              [(ngModel)]="roomName"
              [placeholder]="t('createForm.tableNamePlaceholder', { name: displayName() })"
              maxlength="50"
              [disabled]="submitting()"
            />
          </div>

          <div class="g-divider"></div>

          <!-- Seats -->
          <div class="setting-row seats-head">
            <div>
              <div class="setting-label"><i class="pi pi-users"></i>{{ t('createForm.seats') }}</div>
              <div class="setting-hint">{{ t('createForm.aiHint') }}</div>
            </div>
            <label class="fill-all">
              <span>{{ t('createForm.fillAll') }}</span>
              <p-toggleswitch name="fillAll" [ngModel]="allAi" (ngModelChange)="toggleAllAi()" [disabled]="submitting()" />
            </label>
          </div>

          <div class="seat-grid">
            @for (seat of seatDefs; track seat.key) {
              <div class="seat-cell" [style.grid-area]="seat.area">
                <button
                  type="button"
                  class="seat-toggle"
                  [class.bot]="aiSeats[seat.key]"
                  [disabled]="submitting()"
                  (click)="toggleSeat(seat.key)"
                >
                  <span class="seat-avatar"><i class="pi" [class.pi-microchip]="aiSeats[seat.key]" [class.pi-user-plus]="!aiSeats[seat.key]"></i></span>
                  <span class="seat-name">{{ t(seat.labelKey) }}</span>
                  <span class="seat-kind">{{ aiSeats[seat.key] ? t('createForm.bot') : t('createForm.openSeat') }}</span>
                </button>
                @if (aiSeats[seat.key] && aiTypes().length > 1) {
                  <p-select
                    class="seat-select"
                    [name]="'aiType' + seat.key"
                    [options]="aiTypes()"
                    optionLabel="displayName"
                    optionValue="name"
                    [ngModel]="aiSeats[seat.key]"
                    (ngModelChange)="aiSeats[seat.key] = $event"
                    [disabled]="submitting()"
                    appendTo="body"
                    size="small"
                    fluid
                  />
                }
              </div>
            }
            <div class="seat-cell you" style="grid-area: c">
              <span class="seat-avatar me">{{ initial() }}</span>
              <span class="seat-name">{{ t('common.you') }}</span>
            </div>
          </div>

          <div class="g-divider"></div>

          <!-- Turn timer -->
          <div class="setting-row">
            <div class="setting-label"><i class="pi pi-clock"></i>{{ t('createForm.turnTimer') }}</div>
            <p-selectbutton
              name="turnTimer"
              [options]="timerPresets"
              optionLabel="label"
              optionValue="value"
              [(ngModel)]="selectedTimer"
              [allowEmpty]="false"
              size="small"
              [disabled]="submitting()"
            />
          </div>

          <!-- Invite only -->
          <div class="setting-row">
            <div>
              <div class="setting-label"><i class="pi pi-lock"></i>{{ t('createForm.inviteOnly') }}</div>
              <div class="setting-hint">{{ t('createForm.inviteOnlyHint') }}</div>
            </div>
            <p-toggleswitch name="inviteOnly" [(ngModel)]="inviteOnly" [disabled]="submitting()" />
          </div>

          <!-- Rated -->
          <div class="setting-row">
            <div>
              <div class="setting-label"><i class="pi pi-trophy"></i>{{ t('createForm.rated') }}</div>
              <div class="setting-hint">{{ t('createForm.ratedHint') }}</div>
            </div>
            <p-toggleswitch name="isRanked" [(ngModel)]="isRanked" [disabled]="submitting()" />
          </div>

          @if (error()) {
            <p-message severity="error" icon="pi pi-exclamation-circle" [text]="error()" />
          }
        </form>

        <ng-template #footer>
          <p-button
            [label]="t('common.cancel')"
            severity="secondary"
            [outlined]="true"
            [disabled]="submitting()"
            (onClick)="cancelled.emit()"
          />
          <p-button
            [label]="submitting() ? t('createForm.creating') : t('createForm.createTable')"
            icon="pi pi-check"
            [loading]="submitting()"
            (onClick)="onSubmit()"
          />
        </ng-template>
      </p-dialog>
    </ng-container>
  `,
  styles: [`
    .form { display:flex; flex-direction:column; gap:0.75rem; }
    .field { display:flex; flex-direction:column; gap:0.5rem; }
    .field label { font-weight:500; }
    .seats-head { padding-bottom:0.25rem; }
    .fill-all { display:flex; align-items:center; gap:0.5rem; font-size:0.8125rem; color:var(--text-color-secondary); cursor:pointer; white-space:nowrap; }
    .seat-grid { display:grid; grid-template-columns:1fr 1fr 1fr; grid-template-areas:". n ." "w c e"; gap:0.75rem 0.5rem; padding:0.25rem 0 0.5rem; }
    .seat-cell { display:flex; flex-direction:column; align-items:stretch; gap:0.375rem; min-width:0; }
    .seat-toggle { display:flex; flex-direction:column; align-items:center; gap:0.25rem; padding:0.75rem 0.5rem; border:1px dashed var(--surface-border); border-radius:0.875rem; background:transparent; color:inherit; cursor:pointer; transition:background-color var(--transition-duration), border-color var(--transition-duration); }
    .seat-toggle:hover { background:var(--surface-hover); }
    .seat-toggle.bot { border-style:solid; border-color:color-mix(in srgb, var(--p-primary-color) 45%, transparent); background:color-mix(in srgb, var(--p-primary-color) 10%, transparent); }
    .seat-avatar { display:inline-flex; align-items:center; justify-content:center; width:2.25rem; height:2.25rem; border-radius:50%; background:var(--p-surface-800); color:var(--text-color-secondary); font-weight:700; }
    .bot .seat-avatar { background:color-mix(in srgb, var(--p-primary-color) 25%, transparent); color:var(--p-primary-300); }
    .seat-avatar.me { background:var(--p-primary-color); color:var(--p-primary-contrast-color); }
    .you { align-items:center; justify-content:center; gap:0.25rem; padding:0.75rem 0.5rem; }
    .seat-name { font-size:0.8125rem; font-weight:500; text-align:center; }
    .seat-kind { font-size:0.6875rem; color:var(--text-color-secondary); text-transform:uppercase; letter-spacing:0.04em; }
    @media (max-width:479px) { .seat-grid { grid-template-columns:1fr 1fr; grid-template-areas:"n n" "w e" "c c"; } }
  `],
})
export class CreateRoomFormComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly session = inject(ClientSessionService);
  private readonly auth = inject(AuthService);

  readonly open = input<boolean>(false);

  readonly displayName = () => this.auth.user()?.displayName ?? '';
  readonly initial = computed(() => (this.auth.user()?.displayName ?? '?').charAt(0).toUpperCase());

  readonly seatDefs: SeatDef[] = [
    { key: 'Top', area: 'n', labelKey: 'positions.top' },
    { key: 'Left', area: 'w', labelKey: 'positions.left' },
    { key: 'Right', area: 'e', labelKey: 'positions.right' },
  ];

  readonly roomCreated = output<RoomResponse>();
  readonly cancelled = output<void>();

  roomName = '';
  isRanked = true;
  inviteOnly = false;
  selectedTimer = 30;
  readonly timerPresets = [
    { label: '30s', value: 30 },
    { label: '45s', value: 45 },
    { label: '1m20', value: 80 },
    { label: '3min', value: 180 },
  ];
  aiSeats: Record<SeatKey, string | null> = {
    Left: null,
    Top: null,
    Right: null,
  };
  readonly aiTypes = signal<AiTypeInfo[]>([{ name: DEFAULT_AI_TYPE, displayName: 'Deterministic', difficulty: 0, rating: 0, pun: null, description: null, author: null }]);
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string>('');

  ngOnInit(): void {
    this.api.getAiTypes().subscribe({
      next: (types) => {
        if (types.length > 0) {
          this.aiTypes.set(types);
        }
      },
    });
  }

  get allAi(): boolean {
    return !!this.aiSeats.Left && !!this.aiSeats.Top && !!this.aiSeats.Right;
  }

  toggleSeat(seat: SeatKey): void {
    if (this.aiSeats[seat]) {
      this.aiSeats[seat] = null;
    } else {
      this.aiSeats[seat] = DEFAULT_AI_TYPE;
    }
  }

  toggleAllAi(): void {
    const fill = !this.allAi;
    const type = fill ? DEFAULT_AI_TYPE : null;
    this.aiSeats.Left = type;
    this.aiSeats.Top = type;
    this.aiSeats.Right = type;
  }

  private getAiSeats(): AiSeat[] {
    const seats: AiSeat[] = [];
    if (this.aiSeats.Left) seats.push({ position: PlayerPosition.Left, aiType: this.aiSeats.Left });
    if (this.aiSeats.Top) seats.push({ position: PlayerPosition.Top, aiType: this.aiSeats.Top });
    if (this.aiSeats.Right) seats.push({ position: PlayerPosition.Right, aiType: this.aiSeats.Right });
    return seats;
  }

  onVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelled.emit();
    }
  }

  onSubmit(): void {
    if (this.submitting()) return;
    const name = this.roomName.trim() || null;

    this.submitting.set(true);
    this.error.set('');

    this.api.createRoom(name, this.getAiSeats(), this.selectedTimer, this.inviteOnly, this.isRanked).subscribe({
      next: (response) => {
        this.session.joinRoom(
          response.room.roomId,
          response.clientId,
          response.position
        );

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
