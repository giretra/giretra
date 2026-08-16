import { Component, inject, input } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { LucideAngularModule, Megaphone, Zap, Handshake, Sparkles, Layers } from 'lucide-angular';
import { GameMode } from '../../api/generated/signalr-types.generated';
import {
  HighlightsBidding,
  HighlightsCallout,
  HighlightsPartner,
  HighlightsSweeps,
  HighlightsTricks,
} from '../../core/services/api.service';
import { MODE_LABEL_KEYS } from './highlights-charts.component';

const CARD_STYLES = `
  .card { background: hsl(var(--card)); border: 1px solid hsl(var(--border)); border-radius: var(--radius); padding: 1rem; display: flex; flex-direction: column; gap: 0.75rem; height: 100%; }
  .card-head { display: flex; align-items: center; gap: 0.5rem; color: hsl(var(--muted-foreground)); font-size: 0.8rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; }
  .stat-row { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; font-size: 0.85rem; color: hsl(var(--foreground)); }
  .stat-label { color: hsl(var(--muted-foreground)); }
  .stat-value { font-weight: 600; font-variant-numeric: tabular-nums; }
`;

@Component({
  selector: 'hl-bidding',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="MegaphoneIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.bidding.title') }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.bidding.announceRate') }}</span>
        <span class="stat-value">{{ bidding().announceRate }}%</span>
      </div>
      <div class="bar"><div class="bar-fill" [style.width.%]="bidding().announceRate"></div></div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.bidding.announceWinRate') }}</span>
        <span class="stat-value">{{ bidding().announceWinRate }}%</span>
      </div>
      <div class="bar"><div class="bar-fill" [style.width.%]="bidding().announceWinRate"></div></div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.bidding.doubles') }}</span>
        <span class="stat-value">{{ t('highlights.bidding.won', { won: bidding().doublesWon, made: bidding().doublesMade }) }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.bidding.redoubles') }}</span>
        <span class="stat-value">{{ t('highlights.bidding.won', { won: bidding().redoublesWon, made: bidding().redoublesMade }) }}</span>
      </div>
    </section>
  `,
  styles: [
    CARD_STYLES +
      `
    .bar { height: 6px; border-radius: 3px; background: hsl(var(--muted) / 0.5); overflow: hidden; }
    .bar-fill { height: 100%; border-radius: 3px; background: hsl(var(--gold) / 0.8); }
  `,
  ],
})
export class HlBiddingComponent {
  readonly bidding = input.required<HighlightsBidding>();
  readonly MegaphoneIcon = Megaphone;
}

@Component({
  selector: 'hl-sweeps',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="ZapIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.sweeps.title') }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.sweeps.made') }}</span>
        <span class="stat-value pos">{{ sweeps().sweepsFor }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.sweeps.suffered') }}</span>
        <span class="stat-value neg">{{ sweeps().sweepsAgainst }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.sweeps.instantFor') }}</span>
        <span class="stat-value pos">{{ sweeps().instantWinsFor }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.sweeps.instantAgainst') }}</span>
        <span class="stat-value neg">{{ sweeps().instantWinsAgainst }}</span>
      </div>
    </section>
  `,
  styles: [
    CARD_STYLES +
      `
    .pos { color: hsl(var(--team2)); }
    .neg { color: hsl(var(--destructive)); }
  `,
  ],
})
export class HlSweepsComponent {
  readonly sweeps = input.required<HighlightsSweeps>();
  readonly ZapIcon = Zap;
}

@Component({
  selector: 'hl-tricks',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="LayersIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.tricks.title') }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.tricks.winRate') }}</span>
        <span class="stat-value">{{ tricks().trickWinRate }}%</span>
      </div>
      <p class="hint">{{ t('highlights.tricks.expected') }}</p>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.tricks.won') }}</span>
        <span class="stat-value">{{ tricks().tricksWon }}/{{ tricks().tricksPlayed }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.tricks.lastTrick') }}</span>
        <span class="stat-value">{{ tricks().lastTrickWins }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-label">{{ t('highlights.tricks.bestDeal') }}</span>
        <span class="stat-value">{{ tricks().bestTricksInOneDeal }}/8</span>
      </div>
      @if (tricks().analyzedDeals > 0) {
        <p class="hint">{{ t('highlights.tricks.analyzed', { count: tricks().analyzedDeals }) }}</p>
      }
    </section>
  `,
  styles: [
    CARD_STYLES +
      `
    .hint { font-size: 0.72rem; color: hsl(var(--muted-foreground)); margin: -0.35rem 0 0; }
  `,
  ],
})
export class HlTricksComponent {
  readonly tricks = input.required<HighlightsTricks>();
  readonly LayersIcon = Layers;
}

@Component({
  selector: 'hl-partners',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="HandshakeIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.partners.title') }}</span>
      </div>
      @if (bestPartner(); as p) {
        <div class="partner">
          <span class="avatar avatar-good">{{ p.displayName.charAt(0).toUpperCase() }}</span>
          <div class="partner-info">
            <span class="partner-role">{{ t('highlights.partners.best') }}</span>
            <span class="partner-name">{{ p.displayName }}</span>
            <span class="partner-record">{{ t('highlights.partners.record', { wins: p.wins, games: p.games }) }} · {{ p.winRate }}%</span>
          </div>
        </div>
      }
      @if (nemesis(); as p) {
        <div class="partner">
          <span class="avatar avatar-bad">{{ p.displayName.charAt(0).toUpperCase() }}</span>
          <div class="partner-info">
            <span class="partner-role">{{ t('highlights.partners.nemesis') }}</span>
            <span class="partner-name">{{ p.displayName }}</span>
            <span class="partner-record">{{ t('highlights.partners.record', { wins: p.wins, games: p.games }) }} · {{ p.winRate }}%</span>
          </div>
        </div>
      }
      @if (!bestPartner() && !nemesis()) {
        <p class="not-enough">{{ t('highlights.partners.notEnough') }}</p>
      }
    </section>
  `,
  styles: [
    CARD_STYLES +
      `
    .partner { display: flex; align-items: center; gap: 0.75rem; }
    .avatar { display: flex; align-items: center; justify-content: center; width: 2.25rem; height: 2.25rem; border-radius: 50%; font-weight: 700; color: hsl(var(--foreground)); flex-shrink: 0; }
    .avatar-good { background: hsl(var(--team2) / 0.25); border: 1px solid hsl(var(--team2) / 0.5); }
    .avatar-bad { background: hsl(var(--destructive) / 0.2); border: 1px solid hsl(var(--destructive) / 0.45); }
    .partner-info { display: flex; flex-direction: column; min-width: 0; }
    .partner-role { font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.04em; color: hsl(var(--muted-foreground)); }
    .partner-name { font-weight: 600; font-size: 0.9rem; color: hsl(var(--foreground)); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .partner-record { font-size: 0.78rem; color: hsl(var(--muted-foreground)); }
    .not-enough { font-size: 0.82rem; color: hsl(var(--muted-foreground)); margin: 0; }
  `,
  ],
})
export class HlPartnersComponent {
  readonly bestPartner = input.required<HighlightsPartner | null>();
  readonly nemesis = input.required<HighlightsPartner | null>();
  readonly HandshakeIcon = Handshake;
}

@Component({
  selector: 'hl-callouts',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="SparklesIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.callouts.title') }}</span>
      </div>
      <div class="chips">
        @for (c of callouts(); track c.code) {
          <div class="chip" [class.chip-strength]="c.kind === 'strength'" [class.chip-weakness]="c.kind === 'weakness'">
            <span class="chip-kind">{{ c.kind === 'strength' ? '▲' : '▼' }}</span>
            <span>{{ t('highlights.callouts.' + c.code, { mode: modeLabel(c.mode), value: c.value }) }}</span>
          </div>
        } @empty {
          <p class="not-enough">{{ t('highlights.callouts.empty') }}</p>
        }
      </div>
    </section>
  `,
  styles: [
    CARD_STYLES +
      `
    .chips { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .chip { display: inline-flex; align-items: center; gap: 0.45rem; padding: 0.4rem 0.7rem; border-radius: 9999px; font-size: 0.82rem; color: hsl(var(--foreground)); border: 1px solid hsl(var(--border)); }
    .chip-strength { background: hsl(var(--gold) / 0.12); border-color: hsl(var(--gold) / 0.4); }
    .chip-strength .chip-kind { color: hsl(var(--gold)); }
    .chip-weakness { background: hsl(var(--destructive) / 0.1); border-color: hsl(var(--destructive) / 0.35); }
    .chip-weakness .chip-kind { color: hsl(var(--destructive)); }
    .not-enough { font-size: 0.82rem; color: hsl(var(--muted-foreground)); margin: 0; }
  `,
  ],
})
export class HlCalloutsComponent {
  readonly callouts = input.required<HighlightsCallout[]>();
  readonly SparklesIcon = Sparkles;

  private readonly transloco = inject(TranslocoService);

  modeLabel(mode: GameMode | null): string {
    return mode ? this.transloco.translate(MODE_LABEL_KEYS[mode]) : '';
  }
}
