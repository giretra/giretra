import {
  Component,
  DestroyRef,
  ElementRef,
  NgZone,
  afterNextRender,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { getCardBackSvgHref } from '../../../../../core/utils/card-utils';

/** The server's answer to a committed cut. */
export type CutOutcome =
  | { status: 'confirmed'; position: number }
  | { status: 'error' };

/** Deck geometry: thickness per card and card footprint (svg-cards aspect). */
const CARD = 2.4;
const MIN = 6;
const MAX = 26;
const TOTAL = 32 * CARD;
const DEFAULT_AIM = 16 * CARD;
/** Upper bound for how far the cut packet travels across the felt, world px. */
const MAX_SLIDE = 155;
/** Screen px travelled per world px along +X, given the world's rotation. */
const SLIDE_PROJECTION = 0.85;
/** Depth px per dragged px. */
const DRAG_K = 0.4;

/**
 * A 3D deck the cutter pinches: press and drag vertically to choose how deep
 * to cut, release to commit. Deliberately imprecise - no numbers, no
 * graduations, and the stripe texture can't be counted. Emits the aimed
 * position (6-26); the parent reports back the server's nudged answer via
 * [cutOutcome], which snaps the wedge before the cut animation plays.
 */
@Component({
  selector: 'app-cut-deck-3d',
  standalone: true,
  imports: [TranslocoDirective],
  template: `
    <div class="stage" #stage *transloco="let t">
      <div class="scene">
        <div class="world">
          <div class="shadow"></div>
          <div class="packet3d bottom" #packetBottom>
            <svg class="face f-top" viewBox="0 0 169.075 244.64" preserveAspectRatio="none">
              <use [attr.href]="cardBackHref" />
            </svg>
            <div class="face f-front"></div>
            <div class="face f-side"></div>
          </div>
          <div class="packet3d top" #packetTop>
            <svg class="face f-top" viewBox="0 0 169.075 244.64" preserveAspectRatio="none">
              <use [attr.href]="cardBackHref" />
            </svg>
            <div class="face f-front"></div>
            <div class="face f-side"></div>
          </div>
        </div>
      </div>
      @if (interactive()) {
        <div
          class="deck-input"
          #deckInput
          tabindex="0"
          role="slider"
          [attr.aria-label]="t('cut.dragHint')"
          aria-valuemin="6"
          aria-valuemax="26"
          aria-valuenow="16"
        ></div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      max-width: 420px;
      container-type: inline-size;
    }

    .stage {
      position: relative;
      width: 100%;
      height: 260px;
      border-radius: 12px;
      background:
        radial-gradient(120% 140% at 50% 0%, rgba(255, 255, 255, 0.08), rgba(0, 0, 0, 0) 55%),
        radial-gradient(140% 160% at 50% 110%, rgba(0, 0, 0, 0.30), rgba(0, 0, 0, 0) 60%),
        linear-gradient(180deg, hsl(var(--table-felt-light)), hsl(var(--table-felt)));
      box-shadow: inset 0 0 0 1px rgba(0, 0, 0, 0.25), inset 0 2px 10px rgba(0, 0, 0, 0.18);
      touch-action: none;
      user-select: none;
      -webkit-user-select: none;
      overflow: hidden;
    }

    .scene {
      position: absolute;
      inset: 0;
      perspective: 1200px;
      perspective-origin: 50% 24%;
      pointer-events: none;
    }
    .world {
      position: absolute;
      left: 42%;
      top: 62%;
      width: 0;
      height: 0;
      transform-style: preserve-3d;
      transform: rotateX(58deg) rotateZ(-32deg) scale(var(--deck-scale, 1));
    }

    .shadow {
      position: absolute;
      left: -92px;
      top: -121px;
      width: 190px;
      height: 246px;
      border-radius: 50%;
      background: radial-gradient(ellipse at 50% 50%, rgba(0, 0, 0, 0.38), rgba(0, 0, 0, 0) 68%);
      transform: translateZ(0.4px);
    }

    /* a packet is a cuboid: card-back top + two visible striped edges;
       --t is its thickness in px, driven from the component */
    .packet3d {
      position: absolute;
      left: -63px;
      top: -91px;
      width: 126px;
      height: 182px;
      transform-style: preserve-3d;
      transform-origin: 50% 0;
    }
    .packet3d .face {
      position: absolute;
      left: 0;
      top: 0;
      transform-origin: 0 0;
    }
    .f-top {
      width: 126px;
      height: 182px;
      transform: translateZ(var(--t));
      background: #f6f0df;
      border-radius: 4px;
      box-shadow: inset 0 0 0 1px rgba(0, 0, 0, 0.25);
    }
    /* stripe layers at two periods so single cards can't be counted */
    .f-front, .f-side {
      height: var(--t);
      background-image:
        repeating-linear-gradient(180deg, rgba(112, 94, 58, 0.22) 0 1px, rgba(0, 0, 0, 0) 1px 6.4px),
        repeating-linear-gradient(180deg, #f6f0df 0 1.7px, #d5ccb4 1.7px 2.5px, #eee7d4 2.5px 3.9px, #cbc2ab 3.9px 4.9px);
    }
    .f-front {
      width: 126px;
      transform: translateY(182px) translateZ(var(--t)) rotateX(-90deg);
      filter: brightness(0.94);
    }
    .f-side {
      width: 182px;
      transform: translateZ(var(--t)) rotateX(-90deg) rotateY(-90deg);
      filter: brightness(0.82);
    }
    /* the six-card rims the pinch can't enter */
    .f-front::after, .f-side::after {
      content: "";
      position: absolute;
      left: 0;
      right: 0;
      height: 15px;
      background: rgba(0, 20, 10, 0.18);
      transition: background 0.25s;
    }
    .packet3d.top .f-front::after, .packet3d.top .f-side::after { top: 0; }
    .packet3d.bottom .f-front::after, .packet3d.bottom .f-side::after { bottom: 0; }
    .stage.aiming .f-front::after, .stage.aiming .f-side::after {
      background: rgba(0, 20, 10, 0.38);
    }

    .packet3d.anim { transition: transform 0.5s cubic-bezier(0.3, 0.7, 0.25, 1); }

    .deck-input {
      position: absolute;
      inset: 0;
      cursor: grab;
      outline: none;
    }
    .stage.aiming .deck-input { cursor: grabbing; }
    .deck-input:focus-visible::after {
      content: "";
      position: absolute;
      inset: 6px;
      border: 2px solid hsl(var(--foreground) / 0.7);
      border-radius: 8px;
      pointer-events: none;
    }

    @media (prefers-reduced-motion: reduce) {
      .packet3d.anim { transition: none; }
    }

    /* On a phone the seats squeeze the centre column well under the deck's
       design width, so shrink the deck instead of letting the stage clip it.
       Keep these last: container queries add no specificity, so the base
       .stage / .world rules would otherwise win on source order. */
    @container (max-width: 300px) {
      .stage { --deck-scale: 0.72; height: 205px; }
    }
    @container (max-width: 200px) {
      .stage { --deck-scale: 0.45; height: 170px; }
      .world { left: 50%; top: 64%; }
    }
  `],
})
export class CutDeck3dComponent {
  /** Only the active cutter gets pointer/keyboard input. */
  readonly interactive = input<boolean>(false);
  /** Server outcome for the committed cut; drives the snap + cut animation. */
  readonly cutOutcome = input<CutOutcome | null>(null);
  /** Aimed position (6-26), emitted once when the player releases. */
  readonly committed = output<number>();
  readonly animationDone = output<void>();

  readonly cardBackHref = getCardBackSvgHref();

  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);

  private readonly stageRef = viewChild.required<ElementRef<HTMLElement>>('stage');
  private readonly topRef = viewChild.required<ElementRef<HTMLElement>>('packetTop');
  private readonly botRef = viewChild.required<ElementRef<HTMLElement>>('packetBottom');
  private readonly inputRef = viewChild<ElementRef<HTMLElement>>('deckInput');

  /** Desired pinch depth (top packet thickness, px) and its rendered value. */
  private aim = DEFAULT_AIM;
  private show = DEFAULT_AIM;
  private lift = 0;
  private mode: 'idle' | 'aim' | 'committing' | 'anim' = 'idle';
  private rafId = 0;
  private t0 = 0;
  private animTimeouts: ReturnType<typeof setTimeout>[] = [];
  private readonly reduced =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    afterNextRender(() => {
      this.zone.runOutsideAngular(() => {
        this.t0 = performance.now();
        this.render();
        this.rafId = requestAnimationFrame(this.tick);
      });
    });

    // (Re)attach input listeners whenever the input layer exists
    effect((onCleanup) => {
      const el = this.inputRef()?.nativeElement;
      if (!el) return;
      const controller = new AbortController();
      this.zone.runOutsideAngular(() => this.attachListeners(el, controller.signal));
      onCleanup(() => controller.abort());
    });

    // React to the server's answer for a committed cut
    effect(() => {
      const outcome = this.cutOutcome();
      if (!outcome || this.mode !== 'committing') return;
      if (outcome.status === 'confirmed') {
        this.playCutAnimation(outcome.position);
      } else {
        this.resetToIdle();
      }
    });

    this.destroyRef.onDestroy(() => {
      cancelAnimationFrame(this.rafId);
      this.animTimeouts.forEach(clearTimeout);
    });
  }

  /** Commits a random legal cut - for the "Just cut" button. */
  quickCut(): void {
    if (this.mode !== 'idle' && this.mode !== 'aim') return;
    this.commit(MIN + Math.floor(Math.random() * (MAX - MIN + 1)));
  }

  private readonly tick = (now: number): void => {
    if (this.mode !== 'anim') {
      let target = this.aim;
      if (this.mode === 'aim' && !this.reduced) {
        // Slight hand tremor while aiming
        target += Math.sin((now - this.t0) / 170) * 0.8 + Math.sin((now - this.t0) / 93) * 0.5;
      }
      this.show += (target - this.show) * (this.reduced ? 1 : 0.22);
      const liftTarget = this.mode === 'aim' ? 13 : 0;
      this.lift += (liftTarget - this.lift) * (this.reduced ? 1 : 0.18);
      this.render();
    }
    this.rafId = requestAnimationFrame(this.tick);
  };

  private render(): void {
    const topEl = this.topRef().nativeElement;
    const botEl = this.botRef().nativeElement;
    const botT = TOTAL - this.show;
    topEl.style.setProperty('--t', `${this.show}px`);
    botEl.style.setProperty('--t', `${botT}px`);
    topEl.style.transform = `translateZ(${botT + this.lift}px) rotateX(${this.lift * 0.55}deg)`;
    botEl.style.transform = 'translateZ(0px)';
    this.inputRef()?.nativeElement.setAttribute('aria-valuenow', String(Math.round(this.aim / CARD)));
  }

  private attachListeners(el: HTMLElement, signal: AbortSignal): void {
    let downY = 0;
    let downT = 0;
    let startDepth = 0;
    let moved = false;

    el.addEventListener('pointerdown', (e: PointerEvent) => {
      if (this.mode !== 'idle' && this.mode !== 'aim') return;
      el.setPointerCapture(e.pointerId);
      this.mode = 'aim';
      this.stageRef().nativeElement.classList.add('aiming');
      downY = e.clientY;
      downT = performance.now();
      startDepth = this.aim;
      moved = false;
    }, { signal });

    el.addEventListener('pointermove', (e: PointerEvent) => {
      if (this.mode !== 'aim') return;
      const dy = e.clientY - downY;
      if (Math.abs(dy) > 5) moved = true;
      this.aim = this.clampDepth(startDepth + dy * DRAG_K);
    }, { signal });

    el.addEventListener('pointerup', () => {
      if (this.mode !== 'aim') return;
      const quickTap = !moved && performance.now() - downT < 300;
      this.commit(quickTap
        ? MIN + Math.floor(Math.random() * (MAX - MIN + 1))
        : Math.round(this.aim / CARD));
    }, { signal });

    el.addEventListener('pointercancel', () => {
      if (this.mode !== 'aim') return;
      this.mode = 'idle';
      this.stageRef().nativeElement.classList.remove('aiming');
    }, { signal });

    el.addEventListener('keydown', (e: KeyboardEvent) => {
      if (this.mode !== 'idle' && this.mode !== 'aim') return;
      if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') {
        this.mode = 'aim';
        this.stageRef().nativeElement.classList.add('aiming');
        this.aim = this.clampDepth(this.aim - 2 * CARD);
        e.preventDefault();
      } else if (e.key === 'ArrowDown' || e.key === 'ArrowRight') {
        this.mode = 'aim';
        this.stageRef().nativeElement.classList.add('aiming');
        this.aim = this.clampDepth(this.aim + 2 * CARD);
        e.preventDefault();
      } else if (e.key === 'Enter' || e.key === ' ') {
        this.commit(Math.round(this.aim / CARD));
        e.preventDefault();
      }
    }, { signal });
  }

  private clampDepth(px: number): number {
    return Math.min(MAX * CARD, Math.max(MIN * CARD, px));
  }

  private commit(position: number): void {
    const pos = Math.min(MAX, Math.max(MIN, position));
    this.mode = 'committing';
    this.stageRef().nativeElement.classList.remove('aiming');
    this.aim = pos * CARD;
    this.zone.run(() => this.committed.emit(pos));
  }

  private playCutAnimation(finalPos: number): void {
    this.zone.runOutsideAngular(() => {
      this.mode = 'anim';
      const topEl = this.topRef().nativeElement;
      const botEl = this.botRef().nativeElement;

      // Snap the wedge to the server's nudged answer
      this.aim = this.show = finalPos * CARD;
      this.lift = 0;
      this.render();

      const topT = finalPos * CARD;
      const step = this.reduced ? 60 : 1;
      const slide = this.measureSlide();

      requestAnimationFrame(() => {
        topEl.classList.add('anim');
        botEl.classList.add('anim');
        // 1. top packet comes off, lands on the felt beside the deck
        topEl.style.transform = `translate3d(${slide}px, 0, 0)`;
        // 2. bottom packet goes on top of it
        this.animTimeouts.push(setTimeout(() => {
          botEl.style.transform = `translate3d(${slide}px, 0, ${topT}px)`;
        }, 500 / step));
        // 3. restacked deck slides home
        this.animTimeouts.push(setTimeout(() => {
          topEl.style.transform = 'translate3d(0, 0, 0)';
          botEl.style.transform = `translate3d(0, 0, ${topT}px)`;
        }, 1040 / step));
        // 4. reset to a whole deck and hand control back
        this.animTimeouts.push(setTimeout(() => {
          topEl.classList.remove('anim');
          botEl.classList.remove('anim');
          this.resetToIdle();
          this.zone.run(() => this.animationDone.emit());
        }, 1660 / step));
      });
    });
  }

  /**
   * How far the cut packet may travel, in world px, so it always stays inside
   * the stage. On a phone the stage barely fits the deck, so this shrinks to
   * almost nothing and the cut reads as a lift-and-settle instead of a slide.
   */
  private measureSlide(): number {
    const stage = this.stageRef().nativeElement.getBoundingClientRect();
    const packet = this.topRef().nativeElement.getBoundingClientRect();
    const scale = parseFloat(
      getComputedStyle(this.stageRef().nativeElement).getPropertyValue('--deck-scale')) || 1;
    const roomPx = Math.max(0, (stage.width - packet.width) / 2 - 6);
    return Math.min(MAX_SLIDE, roomPx / (SLIDE_PROJECTION * scale));
  }

  private resetToIdle(): void {
    this.mode = 'idle';
    this.stageRef().nativeElement.classList.remove('aiming');
    this.aim = this.show = DEFAULT_AIM;
    this.lift = 0;
    this.render();
  }
}
