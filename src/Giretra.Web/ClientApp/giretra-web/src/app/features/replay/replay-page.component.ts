/**
 * Route `/replay/match/:matchId/:dealNumber` — interactive replay of one deal, and the page
 * the headless clip renderer loads with `?render=1`.
 *
 * Query params: `t` (ms), `from`/`to` (trick range, 1-based), `view` (seat at the bottom,
 * defaults to the viewer's seat), `hands=all` (show every hand), `render=1` (deterministic
 * frames, no controls, exposes `window.__giretra.seek`), `lang` (en|fr|mg).
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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
} from 'lucide-angular';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { ApiService, DealReplayResponse } from '../../core/services/api.service';
import { ReplayTimeline } from './replay-timeline';
import {
  ReplayStageComponent,
  REPLAY_STAGE_HEIGHT,
  REPLAY_STAGE_WIDTH,
} from './replay-stage.component';

declare global {
  interface Window {
    __giretra?: { seek: (ms: number) => Promise<number>; duration: number };
  }
}

const POSITIONS = new Set<string>(Object.values(PlayerPosition));

@Component({
  selector: 'app-replay-page',
  standalone: true,
  imports: [DecimalPipe, RouterLink, TranslocoDirective, LucideAngularModule, ReplayStageComponent],
  template: `
    <div class="replay-page" [class.render]="renderMode()" *transloco="let t">
      @if (error(); as err) {
        <div class="replay-message">
          <p>{{ t(err) }}</p>
          <a routerLink="/settings" class="replay-link">{{ t('replay.backToHistory') }}</a>
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
              <a class="ctl-btn ghost" routerLink="/settings" [title]="t('replay.backToHistory')">
                <lucide-icon [img]="ArrowLeft" [size]="18" />
              </a>
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
          </div>
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
      .stage-box {
        position: relative;
        overflow: hidden;
        flex: 0 0 auto;
      }
      .stage-fit {
        flex: 1 1 auto;
        width: 100%;
        display: flex;
        align-items: center;
        justify-content: center;
        min-height: 0;
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
      }
      .deal-link {
        color: hsl(var(--foreground));
        text-decoration: none;
      }
      .deal-link:hover {
        text-decoration: underline;
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
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);
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

  readonly fit = viewChild<ElementRef<HTMLElement>>('fit');

  readonly replay = signal<DealReplayResponse | null>(null);
  readonly timeline = signal<ReplayTimeline | null>(null);
  readonly error = signal<string | null>(null);
  readonly time = signal(0);
  readonly playing = signal(false);
  readonly renderMode = signal(false);
  readonly showAllHands = signal(false);
  readonly requestedView = signal<PlayerPosition | null>(null);
  readonly scale = signal(1);

  private resizeObserver: ResizeObserver | null = null;
  private matchId = '';
  private range: { fromTrick?: number; toTrick?: number } = {};
  private rafId: number | null = null;
  private lastTick = 0;

  readonly anchor = computed<PlayerPosition>(() => {
    const requested = this.requestedView();
    if (requested) return requested;
    // Default to the viewer's own seat: the human player whose profile we know, else Bottom.
    return PlayerPosition.Bottom;
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
    this.matchId = p.get('matchId') ?? '';
    const dealNumber = Number(p.get('dealNumber') ?? 1) || 1;

    this.renderMode.set(q.get('render') === '1');
    this.showAllHands.set(q.get('hands') === 'all');
    const view = q.get('view');
    if (view && POSITIONS.has(view)) this.requestedView.set(view as PlayerPosition);
    const from = Number(q.get('from'));
    const to = Number(q.get('to'));
    this.range = {
      fromTrick: Number.isFinite(from) && from > 0 ? from : undefined,
      toTrick: Number.isFinite(to) && to > 0 ? to : undefined,
    };
    const lang = q.get('lang');
    if (
      lang &&
      this.transloco.getAvailableLangs().some((l) => (typeof l === 'string' ? l : l.id) === lang)
    ) {
      this.transloco.setActiveLang(lang);
    }
    const t0 = Number(q.get('t') ?? 0) || 0;

    this.api.getDealReplay(this.matchId, dealNumber).subscribe({
      next: (replay) => {
        const timeline = new ReplayTimeline(replay, this.range);
        this.replay.set(replay);
        this.timeline.set(timeline);
        this.time.set(Math.max(0, Math.min(timeline.duration, t0)));
        if (this.renderMode()) {
          // Expose the seek hook only once the translations are loaded, so the first frame
          // the renderer captures already has its captions.
          this.transloco
            .selectTranslate('replay.dealNumber')
            .pipe(take(1))
            .subscribe(() => {
              window.__giretra = { duration: timeline.duration, seek: (ms) => this.seek(ms) };
            });
        } else if (q.get('autoplay') !== '0') {
          this.togglePlay();
        }
      },
      error: (err) => {
        const status = err?.status;
        this.error.set(
          status === 403
            ? 'replay.errors.forbidden'
            : status === 404
              ? 'replay.errors.notFound'
              : status === 422
                ? 'replay.errors.notReplayable'
                : 'replay.errors.generic',
        );
      },
    });
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
    if (this.renderMode()) return;
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
    const value = Number((event.target as HTMLInputElement).value);
    this.time.set(value);
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
