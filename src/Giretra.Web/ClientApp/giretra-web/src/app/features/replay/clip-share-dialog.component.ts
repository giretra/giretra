/**
 * "Share this deal" dialog: pick a trick range, perspective and language, create the clip,
 * then poll until the render worker has produced the poster/video and offer the link.
 */
import {
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { SelectModule } from 'primeng/select';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { ApiService, ClipResponse } from '../../core/services/api.service';
import { ErrorBannerService } from '../../core/services/error-banner.service';
import { facebookShareUrl, shareFileName, taggedUrl, whatsappShareUrl } from './clip-share-links';

const POLL_MS = 3000;

@Component({
  selector: 'app-clip-share-dialog',
  standalone: true,
  imports: [
    FormsModule,
    TranslocoDirective,
    DialogModule,
    ButtonModule,
    ToggleSwitchModule,
    SelectModule,
  ],
  template: `
    <ng-container *transloco="let t">
      <p-dialog
        [visible]="open()"
        (visibleChange)="onVisibleChange($event)"
        [modal]="true"
        [draggable]="false"
        [dismissableMask]="true"
        [style]="{ width: '28rem' }"
        [breakpoints]="{ '640px': '95vw' }"
        [header]="t('clip.share.title')"
      >
        @if (!clip()) {
          <div class="share-form">
            <div class="field">
              <span class="label">{{ t('clip.share.tricks') }}</span>
              <div class="chips">
                @for (n of tricks; track n) {
                  <button
                    type="button"
                    class="chip"
                    [class.on]="n >= from() && n <= to()"
                    [class.edge]="n === from() || n === to()"
                    (click)="pick(n)"
                  >
                    {{ n }}
                  </button>
                }
              </div>
              <span class="hint"
                >{{ t('clip.share.from') }} {{ from() }} · {{ t('clip.share.to') }} {{ to() }}</span
              >
            </div>
            <label class="field row">
              <span class="label">{{ t('clip.share.allHands') }}</span>
              <p-toggleswitch [ngModel]="allHands()" (ngModelChange)="allHands.set($event)" />
            </label>
            <div class="field row">
              <span class="label">{{ t('clip.share.language') }}</span>
              <p-select
                [options]="languages"
                [ngModel]="locale()"
                (ngModelChange)="locale.set($event)"
                optionLabel="label"
                optionValue="code"
                styleClass="lang"
              />
            </div>
            <p-button
              [label]="creating() ? t('clip.share.creating') : t('clip.share.create')"
              [loading]="creating()"
              [disabled]="creating()"
              (onClick)="create()"
              styleClass="w-full"
            />
          </div>
        } @else {
          <div class="share-result">
            <p class="title">{{ clip()!.title }}</p>
            @switch (clip()!.status) {
              @case ('Ready') {
                <p class="state ok">{{ t('clip.share.ready') }}</p>
              }
              @case ('Failed') {
                <p class="state bad">{{ t('clip.share.failed') }}</p>
              }
              @default {
                <p class="state">{{ t('clip.share.preparing') }}</p>
              }
            }
            @if (clip()!.videoUrl; as video) {
              <video
                class="poster"
                controls
                playsinline
                preload="metadata"
                [poster]="clip()!.posterUrl ?? ''"
                [src]="video"
              ></video>
            } @else if (clip()!.posterUrl; as poster) {
              <img class="poster" [src]="poster" alt="" />
            }
            <div class="targets">
              <a class="target whatsapp" [href]="whatsappUrl()" target="_blank" rel="noopener">
                <i class="pi pi-whatsapp"></i>
                <span>WhatsApp</span>
              </a>
              <a class="target facebook" [href]="facebookUrl()" target="_blank" rel="noopener">
                <i class="pi pi-facebook"></i>
                <span>Facebook</span>
              </a>
              @if (canNativeShare) {
                <button type="button" class="target" (click)="nativeShare()" [disabled]="sharing()">
                  <i
                    class="pi"
                    [class.pi-share-alt]="!sharing()"
                    [class.pi-spinner]="sharing()"
                    [class.pi-spin]="sharing()"
                  ></i>
                  <span>{{ t('clip.share.more') }}</span>
                </button>
              }
              <button type="button" class="target" [class.done]="copied()" (click)="copy()">
                <i class="pi" [class.pi-copy]="!copied()" [class.pi-check]="copied()"></i>
                <span>{{ copied() ? t('clip.share.copied') : t('clip.share.copy') }}</span>
              </button>
            </div>
            <input
              class="link"
              type="text"
              readonly
              [value]="clip()!.pageUrl"
              (focus)="selectAll($event)"
            />
            <div class="actions">
              <a
                class="p-button p-button-text"
                [href]="clip()!.pageUrl"
                target="_blank"
                rel="noopener"
                >{{ t('clip.share.open') }}</a
              >
              @if (clip()!.videoUrl; as video) {
                <a class="p-button" [href]="video + '?download=1'" download>{{
                  t('clip.share.download')
                }}</a>
              }
              <p-button
                [label]="t('clip.share.close')"
                severity="secondary"
                [text]="true"
                (onClick)="close()"
              />
            </div>
          </div>
        }
      </p-dialog>
    </ng-container>
  `,
  styles: [
    `
      .share-form,
      .share-result {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .field.row {
        flex-direction: row;
        align-items: center;
        justify-content: space-between;
      }
      .label {
        font-weight: 600;
        font-size: 0.9rem;
      }
      .hint {
        font-size: 0.8rem;
        color: var(--p-text-muted-color);
      }
      .chips {
        display: flex;
        gap: 0.375rem;
      }
      .chip {
        flex: 1;
        height: 2.25rem;
        border-radius: 0.5rem;
        border: 1px solid var(--p-surface-600);
        background: var(--p-surface-800);
        color: inherit;
        cursor: pointer;
        font-weight: 600;
      }
      .chip.on {
        background: var(--p-primary-color);
        color: var(--p-primary-contrast-color);
        border-color: transparent;
        opacity: 0.75;
      }
      .chip.edge {
        opacity: 1;
      }
      .title {
        font-weight: 600;
        margin: 0;
      }
      .state {
        margin: 0;
        color: var(--p-text-muted-color);
      }
      .state.ok {
        color: var(--p-green-400);
      }
      .state.bad {
        color: var(--p-red-400);
      }
      .poster {
        width: 100%;
        max-height: 40vh;
        object-fit: contain;
        border-radius: 0.75rem;
        background: #000;
      }
      .targets {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(5.5rem, 1fr));
        gap: 0.5rem;
      }
      .target {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 0.375rem;
        padding: 0.75rem 0.25rem;
        border-radius: 0.75rem;
        border: 1px solid var(--p-surface-600);
        background: var(--p-surface-800);
        color: inherit;
        font: inherit;
        font-size: 0.8rem;
        font-weight: 600;
        text-decoration: none;
        cursor: pointer;
      }
      .target .pi {
        font-size: 1.5rem;
      }
      .target:hover {
        filter: brightness(1.15);
      }
      .target:disabled {
        opacity: 0.6;
        cursor: progress;
      }
      .target.whatsapp {
        background: #1f8f4d;
        border-color: transparent;
        color: #fff;
      }
      .target.facebook {
        background: #1877f2;
        border-color: transparent;
        color: #fff;
      }
      .target.done {
        border-color: var(--p-green-400);
        color: var(--p-green-400);
      }
      .link {
        width: 100%;
        min-width: 0;
        padding: 0.5rem 0.75rem;
        border-radius: 0.5rem;
        border: 1px solid var(--p-surface-600);
        background: var(--p-surface-900);
        color: inherit;
        font-size: 0.85rem;
      }
      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        justify-content: flex-end;
        align-items: center;
      }
      .actions a {
        text-decoration: none;
      }
    `,
  ],
})
export class ClipShareDialogComponent {
  private readonly api = inject(ApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly errorBanner = inject(ErrorBannerService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = input(false);
  readonly matchId = input.required<string>();
  readonly dealNumber = input.required<number>();
  readonly suggestedTrick = input<number | null>(null);
  /** Preset trick range (wins over suggestedTrick), e.g. a solver-suggested moment. */
  readonly suggestedRange = input<readonly [number, number] | null>(null);
  readonly viewPosition = input<PlayerPosition | null>(null);
  readonly initialAllHands = input(false);
  readonly openChange = output<boolean>();
  readonly created = output<ClipResponse>();

  readonly tricks = [1, 2, 3, 4, 5, 6, 7, 8];
  readonly languages = [
    { code: 'mg', label: 'Malagasy' },
    { code: 'fr', label: 'Français' },
    { code: 'en', label: 'English' },
  ];

  readonly from = signal(1);
  readonly to = signal(8);
  readonly allHands = signal(false);
  readonly locale = signal(this.transloco.getActiveLang());
  readonly creating = signal(false);
  readonly clip = signal<ClipResponse | null>(null);
  readonly copied = signal(false);
  readonly sharing = signal(false);

  /** Web Share API (phones, and some desktop browsers); the system sheet reaches TikTok, Instagram, Telegram… */
  readonly canNativeShare =
    typeof navigator !== 'undefined' && typeof navigator.share === 'function';

  readonly whatsappUrl = computed(() => {
    const clip = this.clip();
    return clip ? whatsappShareUrl(clip.pageUrl, this.shareMessage(clip)) : '';
  });
  readonly facebookUrl = computed(() => {
    const clip = this.clip();
    return clip ? facebookShareUrl(clip.pageUrl) : '';
  });

  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private anchor: number | null = null;

  constructor() {
    // Reset the form every time the dialog opens, seeded with the trick on screen. Only the
    // open flag is tracked: the replay keeps playing behind the dialog, and a change of the
    // suggested trick must not wipe a clip the user is about to share.
    effect(() => {
      if (!this.open()) return;
      untracked(() => {
        const range = this.suggestedRange();
        const suggested = this.suggestedTrick();
        this.from.set(range?.[0] ?? suggested ?? 1);
        this.to.set(range?.[1] ?? suggested ?? 8);
        this.anchor = null;
        this.allHands.set(this.initialAllHands());
        this.locale.set(this.transloco.getActiveLang());
        this.clip.set(null);
        this.copied.set(false);
      });
    });
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  /** First tap sets both ends, second tap extends the range. */
  pick(n: number): void {
    if (this.anchor === null) {
      this.anchor = n;
      this.from.set(n);
      this.to.set(n);
    } else {
      this.from.set(Math.min(this.anchor, n));
      this.to.set(Math.max(this.anchor, n));
      this.anchor = null;
    }
  }

  create(): void {
    this.creating.set(true);
    this.api
      .createClip({
        matchId: this.matchId(),
        dealNumber: this.dealNumber(),
        fromTrick: this.from(),
        toTrick: this.to(),
        viewPosition: this.viewPosition(),
        showAllHands: this.allHands(),
        locale: this.locale(),
      })
      .subscribe({
        next: (clip) => {
          this.creating.set(false);
          this.clip.set(clip);
          this.created.emit(clip);
          this.schedulePoll();
        },
        error: () => this.creating.set(false),
      });
  }

  /**
   * System share sheet. With the video ready and file sharing supported, the MP4 goes along
   * (TikTok, Instagram and WhatsApp status take files, not links); otherwise the link alone.
   */
  async nativeShare(): Promise<void> {
    const clip = this.clip();
    if (!clip || this.sharing()) return;
    this.sharing.set(true);
    const url = taggedUrl(clip.pageUrl, 'native');
    const text = `${this.shareMessage(clip)}\n${url}`;
    try {
      let data: ShareData = { title: clip.title, text, url };
      if (clip.videoUrl && typeof navigator.canShare === 'function') {
        const file = await this.fetchVideoFile(clip.videoUrl, clip.shortId);
        if (file && navigator.canShare({ files: [file] }))
          data = { title: clip.title, text, files: [file] };
      }
      await navigator.share(data);
    } catch (e) {
      if (!(e instanceof DOMException && e.name === 'AbortError'))
        this.errorBanner.show(this.transloco.translate('clip.share.shareFailed'));
    } finally {
      this.sharing.set(false);
    }
  }

  private async fetchVideoFile(videoUrl: string, shortId: string): Promise<File | null> {
    try {
      const res = await fetch(videoUrl);
      if (!res.ok) return null;
      const blob = await res.blob();
      return new File([blob], shareFileName(shortId), { type: 'video/mp4' });
    } catch {
      return null;
    }
  }

  private shareMessage(clip: ClipResponse): string {
    return this.transloco.translate('clip.share.message', { title: clip.title });
  }

  async copy(): Promise<void> {
    const clip = this.clip();
    if (!clip) return;
    try {
      await navigator.clipboard.writeText(taggedUrl(clip.pageUrl, 'copy'));
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      this.errorBanner.show(this.transloco.translate('home.linkCopyFailed'));
    }
  }

  selectAll(event: FocusEvent): void {
    (event.target as HTMLInputElement).select();
  }

  close(): void {
    this.onVisibleChange(false);
  }

  onVisibleChange(visible: boolean): void {
    if (!visible) this.stopPolling();
    this.openChange.emit(visible);
  }

  private schedulePoll(): void {
    this.stopPolling();
    const clip = this.clip();
    if (!clip || clip.status === 'Ready' || clip.status === 'Failed' || clip.status === 'Removed')
      return;
    this.pollTimer = setTimeout(() => {
      this.api.getClip(clip.shortId).subscribe({
        next: (fresh) => {
          this.clip.set({ ...fresh, replay: null });
          this.schedulePoll();
        },
        error: () => this.schedulePoll(),
      });
    }, POLL_MS);
  }

  private stopPolling(): void {
    if (this.pollTimer) clearTimeout(this.pollTimer);
    this.pollTimer = null;
  }
}
