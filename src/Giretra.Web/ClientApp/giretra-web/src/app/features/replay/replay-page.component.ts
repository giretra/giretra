/**
 * Deal replay page. Two sources:
 *  - `/replay/match/:matchId/:dealNumber` — participants, authenticated; offers "Share".
 *  - `/replay/clip/:shortId` — public, anonymous (AuthService skips Keycloak on this path);
 *    range, perspective and language come from the clip.
 *
 * Query params: `t` (ms), `from`/`to` (trick range), `view` (seat at the bottom),
 * `hands=all`, `render=1` (deterministic frames, no controls, exposes
 * `window.__giretra.seek` for the clip renderer), `lang`, `autoplay=0`.
 */
import {
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  HostListener,
  inject,
  NgZone,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { take } from 'rxjs/operators';
import {
  LucideAngularModule,
  Pause,
  Play,
  SkipBack,
  SkipForward,
  ArrowLeft,
  Eye,
  EyeOff,
  Share2,
} from 'lucide-angular';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { ApiService, ClipResponse, DealReplayResponse } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { ReplayTimeline, ReplaySoundEvent } from './replay-timeline';
import {
  ReplayStageComponent,
  REPLAY_STAGE_HEIGHT,
  REPLAY_STAGE_WIDTH,
} from './replay-stage.component';
import { ClipShareDialogComponent } from './clip-share-dialog.component';

declare global {
  interface Window {
    __giretra?: {
      seek: (ms: number) => Promise<number>;
      duration: number;
      posterTime: number;
      events: ReplaySoundEvent[];
    };
  }
}

const POSITIONS = new Set<string>(Object.values(PlayerPosition));

@Component({
  selector: 'app-replay-page',
  standalone: true,
  imports: [
    DecimalPipe,
    RouterLink,
    TranslocoDirective,
    LucideAngularModule,
    ReplayStageComponent,
    ClipShareDialogComponent,
  ],
  template: `
    <div class="replay-page" [class.render]="renderMode()" *transloco="let t">
      @if (error(); as err) {
        <div class="replay-message">
          <p>{{ t(err) }}</p>
          @if (isClip()) {
            <a href="/" class="replay-link">{{ t('replay.playCta') }}</a>
          } @else {
            <a routerLink="/settings" class="replay-link">{{ t('replay.backToHistory') }}</a>
          }
        </div>
      } @else if (!replay() || !timeline()) {
        <div class="replay-message">{{ t('common.loading') }}</div>
      } @else {
        <div class="stage-fit" #fit>
          <div class="stage-box" [style.width.px]="540 * scale()" [style.height.px]="960 * scale()">
            <div class="stage-scale" [style.transform]="'scale(' + scale() + ')'">
              <app-replay-stage
                [replay]="replay()!"
                [timeline]="timeline()!"
                [t]="time()"
                [anchor]="anchor()"
                [showAllHands]="showAllHands()"
                [renderMode]="renderMode()"
              />
            </div>
          </div>
        </div>

        @if (!renderMode()) {
          <div class="controls">
            <div class="controls-row">
              @if (!isClip()) {
                <a
                  class="ctl-btn ghost"
                  routerLink="/settings"
                  [queryParams]="{ section: 'history' }"
                  [title]="t('replay.backToHistory')"
                >
                  <lucide-icon [img]="ArrowLeft" [size]="18" />
                </a>
              }
              <button
                type="button"
                class="ctl-btn"
                (click)="jumpTrick(-1)"
                [title]="t('replay.prevTrick')"
              >
                <lucide-icon [img]="SkipBack" [size]="18" />
              </button>
              <button
                type="button"
                class="ctl-btn primary"
                (click)="togglePlay()"
                [title]="playing() ? t('replay.pause') : t('replay.play')"
              >
                <lucide-icon [img]="playing() ? Pause : Play" [size]="20" />
              </button>
              <button
                type="button"
                class="ctl-btn"
                (click)="jumpTrick(1)"
                [title]="t('replay.nextTrick')"
              >
                <lucide-icon [img]="SkipForward" [size]="18" />
              </button>
              <button
                type="button"
                class="ctl-btn"
                (click)="toggleHands()"
                [title]="showAllHands() ? t('replay.hideHands') : t('replay.showHands')"
              >
                <lucide-icon [img]="showAllHands() ? EyeOff : Eye" [size]="18" />
              </button>
              <span class="ctl-time">{{ time() / 1000 | number: '1.1-1' }}s</span>
            </div>
            <input
              class="scrubber"
              type="range"
              min="0"
              [max]="timeline()!.duration"
              step="10"
              [value]="time()"
              (input)="onScrub($event)"
            />
            @if (canShare()) {
              <button type="button" class="share-cta" (click)="shareOpen.set(true)">
                <lucide-icon [img]="Share2" [size]="18" />
                <span>{{ t('clip.share.cta') }}</span>
              </button>
            }
            @if (isClip()) {
              <div class="controls-row deals">
                <span class="deal-current">{{ clip()?.title }}</span>
                <a class="deal-link cta" href="/?utm_source=replay&utm_medium=share">{{
                  t('replay.playCta')
                }}</a>
              </div>
            } @else {
              <div class="controls-row deals">
                @if (replay()!.dealNumber > 1) {
                  <a
                    class="deal-link"
                    [routerLink]="dealLink(replay()!.dealNumber - 1)"
                    [queryParams]="dealQuery()"
                    >‹ {{ t('replay.dealNumber', { number: replay()!.dealNumber - 1 }) }}</a
                  >
                }
                <span class="deal-current"
                  >{{ t('replay.dealNumber', { number: replay()!.dealNumber }) }} /
                  {{ replay()!.totalDeals }}</span
                >
                @if (replay()!.dealNumber < replay()!.totalDeals) {
                  <a
                    class="deal-link"
                    [routerLink]="dealLink(replay()!.dealNumber + 1)"
                    [queryParams]="dealQuery()"
                    >{{ t('replay.dealNumber', { number: replay()!.dealNumber + 1 }) }} ›</a
                  >
                }
              </div>
            }
          </div>

          @if (canShare()) {
            <app-clip-share-dialog
              [open]="shareOpen()"
              (openChange)="shareOpen.set($event)"
              [matchId]="matchId"
              [dealNumber]="replay()!.dealNumber"
              [suggestedTrick]="currentTrickNumber()"
              [viewPosition]="anchor()"
              [initialAllHands]="showAllHands()"
            />
          }
        }
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100dvh;
        width: 100vw;
        background: hsl(var(--background));
        color: hsl(var(--foreground));
      }
      .replay-page {
        height: 100%;
        display: flex;
        flex-direction: column;
        align-items: center;
        overflow: hidden;
      }
      .replay-page.render {
        width: 540px;
        height: 960px;
        display: block;
        overflow: hidden;
      }
      .replay-page.render .stage-fit {
        width: 540px;
        height: 960px;
        display: block;
      }
      .replay-page.render .stage-box {
        width: 540px !important;
        height: 960px !important;
      }
      .replay-page.render .stage-scale {
        transform: none !important;
      }
      .stage-fit {
        flex: 1 1 auto;
        width: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        min-height: 0;
      }
      .stage-box {
        position: relative;
        overflow: hidden;
        flex: 0 0 auto;
      }
      .stage-scale {
        width: 540px;
        height: 960px;
        transform-origin: top left;
      }
      .controls {
        flex: 0 0 auto;
        width: min(100%, 540px);
        padding: 0.5rem 1rem 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }
      .controls-row {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
      }
      .controls-row.deals {
        font-size: 0.85rem;
        color: hsl(var(--muted-foreground));
        gap: 1rem;
        flex-wrap: wrap;
        text-align: center;
      }
      .deal-link {
        color: hsl(var(--foreground));
        text-decoration: none;
      }
      .deal-link:hover {
        text-decoration: underline;
      }
      .deal-link.cta {
        color: hsl(var(--gold));
        font-weight: 600;
      }
      .ctl-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.5rem;
        height: 2.5rem;
        border-radius: 9999px;
        border: 1px solid hsl(var(--border));
        background: hsl(var(--card));
        color: inherit;
        cursor: pointer;
      }
      .ctl-btn.primary {
        width: 3rem;
        height: 3rem;
        background: hsl(var(--gold));
        color: hsl(var(--background));
        border-color: transparent;
      }
      .share-cta {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        width: 100%;
        min-height: 2.75rem;
        padding: 0 1rem;
        border-radius: 9999px;
        border: none;
        background: hsl(var(--gold));
        color: hsl(var(--background));
        font-weight: 600;
        font-size: 0.95rem;
        cursor: pointer;
        box-shadow: 0 2px 10px hsl(var(--gold) / 0.35);
      }
      .share-cta:hover {
        filter: brightness(1.08);
      }
      .share-cta:active {
        transform: translateY(1px);
      }
      .ctl-btn.ghost {
        border-color: transparent;
        background: transparent;
      }
      .ctl-time {
        font-variant-numeric: tabular-nums;
        font-size: 0.85rem;
        color: hsl(var(--muted-foreground));
        min-width: 3.5rem;
        text-align: right;
      }
      .scrubber {
        width: 100%;
        accent-color: hsl(var(--gold));
      }
      .replay-message {
        flex: 1;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 1rem;
        color: hsl(var(--muted-foreground));
      }
      .replay-link {
        color: hsl(var(--gold));
      }
    `,
  ],
})
export class ReplayPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly zone = inject(NgZone);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  readonly Play = Play;
  readonly Pause = Pause;
  readonly SkipBack = SkipBack;
  readonly SkipForward = SkipForward;
  readonly ArrowLeft = ArrowLeft;
  readonly Eye = Eye;
  readonly EyeOff = EyeOff;
  readonly Share2 = Share2;

  readonly fit = viewChild<ElementRef<HTMLElement>>('fit');

  readonly isClip = signal(false);
  readonly clip = signal<ClipResponse | null>(null);
  readonly replay = signal<DealReplayResponse | null>(null);
  readonly timeline = signal<ReplayTimeline | null>(null);
  readonly error = signal<string | null>(null);
  readonly time = signal(0);
  readonly playing = signal(false);
  readonly renderMode = signal(false);
  readonly showAllHands = signal(false);
  readonly requestedView = signal<PlayerPosition | null>(null);
  readonly scale = signal(1);
  readonly shareOpen = signal(false);

  matchId = '';
  private range: { fromTrick?: number; toTrick?: number } = {};
  private rafId: number | null = null;
  private lastTick = 0;
  private resizeObserver: ResizeObserver | null = null;

  readonly anchor = computed<PlayerPosition>(() => this.requestedView() ?? PlayerPosition.Bottom);
  readonly canShare = computed(() => !this.isClip() && !this.auth.anonymous());
  readonly currentTrickNumber = computed(() => {
    const tl = this.timeline();
    return tl ? tl.frameAt(this.time()).trickNumber : null;
  });

  constructor() {
    // The fit box only exists once the replay is loaded; measure it then and on every resize.
    effect(() => {
      const host = this.fit()?.nativeElement;
      if (!host || this.renderMode()) return;
      this.resizeObserver?.disconnect();
      this.resizeObserver = new ResizeObserver(() => this.updateScale());
      this.resizeObserver.observe(host);
      this.updateScale();
    });
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const p = this.route.snapshot.paramMap;
    this.isClip.set(this.route.snapshot.data['source'] === 'clip');
    this.renderMode.set(q.get('render') === '1');

    const view = q.get('view');
    if (view && POSITIONS.has(view)) this.requestedView.set(view as PlayerPosition);
    if (q.get('hands') === 'all') this.showAllHands.set(true);
    const from = Number(q.get('from'));
    const to = Number(q.get('to'));
    this.range = {
      fromTrick: Number.isFinite(from) && from > 0 ? from : undefined,
      toTrick: Number.isFinite(to) && to > 0 ? to : undefined,
    };
    const lang = q.get('lang');
    if (lang) this.setLanguage(lang);
    const t0 = Number(q.get('t') ?? 0) || 0;
    const autoplay = q.get('autoplay') !== '0';

    if (this.isClip()) {
      const shortId = p.get('shortId') ?? '';
      this.api.getClip(shortId).subscribe({
        next: (clip) => {
          if (!clip.replay) {
            this.error.set('replay.errors.clipGone');
            return;
          }
          this.clip.set(clip);
          this.matchId = clip.matchId;
          // The clip defines the defaults; query params may still override them.
          if (!view) this.requestedView.set(clip.viewPosition);
          if (q.get('hands') === null) this.showAllHands.set(clip.showAllHands);
          if (!lang) this.setLanguage(clip.locale);
          this.range = {
            fromTrick: this.range.fromTrick ?? clip.fromTrick,
            toTrick: this.range.toTrick ?? clip.toTrick,
          };
          this.start(clip.replay, t0, autoplay);
        },
        error: (err) =>
          this.error.set(err?.status === 410 ? 'replay.errors.clipGone' : this.errorKey(err)),
      });
    } else {
      this.matchId = p.get('matchId') ?? '';
      const dealNumber = Number(p.get('dealNumber') ?? 1) || 1;
      this.api.getDealReplay(this.matchId, dealNumber).subscribe({
        next: (replay) => this.start(replay, t0, autoplay),
        error: (err) => this.error.set(this.errorKey(err)),
      });
    }
  }

  ngOnDestroy(): void {
    this.stopLoop();
    this.resizeObserver?.disconnect();
    delete window.__giretra;
  }

  @HostListener('window:resize')
  updateScale(): void {
    if (this.renderMode()) return;
    const host = this.fit()?.nativeElement;
    if (!host) return;
    const s = Math.min(
      host.clientWidth / REPLAY_STAGE_WIDTH,
      host.clientHeight / REPLAY_STAGE_HEIGHT,
      1.25,
    );
    this.scale.set(Math.max(0.2, s));
  }

  @HostListener('document:keydown.space', ['$event'])
  onSpace(event: Event): void {
    if (this.renderMode() || this.shareOpen()) return;
    event.preventDefault();
    this.togglePlay();
  }

  togglePlay(): void {
    if (this.playing()) {
      this.stopLoop();
      return;
    }
    const timeline = this.timeline();
    if (!timeline) return;
    if (this.time() >= timeline.duration) this.time.set(0);
    this.playing.set(true);
    this.lastTick = performance.now();
    this.zone.runOutsideAngular(() => {
      const loop = (now: number) => {
        const dt = now - this.lastTick;
        this.lastTick = now;
        const next = Math.min(timeline.duration, this.time() + dt);
        this.zone.run(() => this.time.set(next));
        if (next >= timeline.duration) {
          this.zone.run(() => this.playing.set(false));
          this.rafId = null;
          return;
        }
        this.rafId = requestAnimationFrame(loop);
      };
      this.rafId = requestAnimationFrame(loop);
    });
  }

  jumpTrick(delta: number): void {
    const timeline = this.timeline();
    if (!timeline) return;
    const current =
      timeline.frameAt(this.time()).trickNumber ??
      (this.time() < timeline.timing.intro
        ? timeline.range.fromTrick - 1
        : timeline.range.toTrick + 1);
    const target = current + delta;
    if (target < timeline.range.fromTrick) {
      this.time.set(0);
    } else if (target > timeline.range.toTrick) {
      this.time.set(timeline.duration - timeline.timing.outro);
    } else {
      this.time.set(timeline.trickStart(target));
    }
  }

  toggleHands(): void {
    this.showAllHands.update((v) => !v);
  }

  onScrub(event: Event): void {
    this.time.set(Number((event.target as HTMLInputElement).value));
  }

  dealLink(dealNumber: number): string[] {
    return ['/replay/match', this.matchId, String(dealNumber)];
  }

  dealQuery(): Record<string, string> {
    const q: Record<string, string> = {};
    const view = this.requestedView();
    if (view) q['view'] = view;
    if (this.showAllHands()) q['hands'] = 'all';
    return q;
  }

  private start(replay: DealReplayResponse, t0: number, autoplay: boolean): void {
    const timeline = new ReplayTimeline(replay, this.range);
    this.replay.set(replay);
    this.timeline.set(timeline);
    this.time.set(Math.max(0, Math.min(timeline.duration, t0)));

    if (this.renderMode()) {
      // Expose the seek hook only once the translations are loaded, so the first frame the
      // renderer captures already has its captions.
      this.transloco
        .selectTranslate('replay.dealNumber')
        .pipe(take(1))
        .subscribe(() => {
          window.__giretra = {
            duration: timeline.duration,
            posterTime: ReplayPageComponent.posterTime(timeline),
            events: timeline.soundEvents(),
            seek: (ms) => this.seek(ms),
          };
        });
    } else if (autoplay) {
      this.togglePlay();
    }
  }

  /** Frame used as the clip's poster: the first trick of the range complete, winner highlighted. */
  static posterTime(timeline: ReplayTimeline): number {
    const { card, entry } = timeline.timing;
    return timeline.trickStart(timeline.range.fromTrick) + 3 * card + entry + 100;
  }

  private setLanguage(lang: string): void {
    const available = this.transloco
      .getAvailableLangs()
      .map((l) => (typeof l === 'string' ? l : l.id));
    if (available.includes(lang) && this.transloco.getActiveLang() !== lang)
      this.transloco.setActiveLang(lang);
  }

  private errorKey(err: { status?: number } | null): string {
    switch (err?.status) {
      case 403:
        return 'replay.errors.forbidden';
      case 404:
        return 'replay.errors.notFound';
      case 422:
        return 'replay.errors.notReplayable';
      default:
        return 'replay.errors.generic';
    }
  }

  private stopLoop(): void {
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    this.rafId = null;
    this.playing.set(false);
  }

  /** Render hook: set t inside the zone, resolve after fonts are ready and two frames painted. */
  private seek(ms: number): Promise<number> {
    this.zone.run(() => this.time.set(ms));
    return document.fonts.ready.then(
      () =>
        new Promise<number>((resolve) =>
          requestAnimationFrame(() => requestAnimationFrame(() => resolve(ms))),
        ),
    );
  }
}
