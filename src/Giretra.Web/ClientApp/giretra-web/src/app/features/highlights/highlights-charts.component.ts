import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { LucideAngularModule, Radar, TrendingUp, CalendarDays } from 'lucide-angular';
import { GameMode } from '../../api/generated/signalr-types.generated';
import {
  HighlightsModeStats,
  HighlightsEloPoint,
  HighlightsActivityDay,
} from '../../core/services/api.service';
import { EchartComponent } from './echart.component';
import { echarts, CHART, CHART_TOOLTIP, EChartsOption } from './echarts-setup';

export const MODE_ORDER: GameMode[] = [
  GameMode.ColourClubs,
  GameMode.ColourDiamonds,
  GameMode.ColourHearts,
  GameMode.ColourSpades,
  GameMode.NoTrumps,
  GameMode.AllTrumps,
];

export const MODE_LABEL_KEYS: Record<GameMode, string> = {
  [GameMode.ColourClubs]: 'game.modes.clubs',
  [GameMode.ColourDiamonds]: 'game.modes.diamonds',
  [GameMode.ColourHearts]: 'game.modes.hearts',
  [GameMode.ColourSpades]: 'game.modes.spades',
  [GameMode.NoTrumps]: 'game.modes.noTrumps',
  [GameMode.AllTrumps]: 'game.modes.allTrumps',
};

const MODE_SYMBOLS: Record<GameMode, string> = {
  [GameMode.ColourClubs]: '♣',
  [GameMode.ColourDiamonds]: '♦',
  [GameMode.ColourHearts]: '♥',
  [GameMode.ColourSpades]: '♠',
  [GameMode.NoTrumps]: '∅',
  [GameMode.AllTrumps]: '★',
};

const CARD_STYLES = `
  .card { background: hsl(var(--card)); border: 1px solid hsl(var(--border)); border-radius: var(--radius); padding: 1rem; display: flex; flex-direction: column; gap: 0.75rem; }
  .card-head { display: flex; align-items: center; gap: 0.5rem; color: hsl(var(--muted-foreground)); font-size: 0.8rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; }
`;

@Component({
  selector: 'hl-radar',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, EchartComponent],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="RadarIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.radar.title') }}</span>
      </div>
      <div class="chart-box"><app-echart [option]="option()" /></div>
    </section>
  `,
  styles: [CARD_STYLES + `.chart-box { height: 320px; }`],
})
export class HlRadarComponent {
  readonly modeStats = input.required<HighlightsModeStats[]>();
  readonly RadarIcon = Radar;

  private readonly transloco = inject(TranslocoService);
  private readonly lang = signal('');

  constructor() {
    this.transloco.langChanges$.pipe(takeUntilDestroyed()).subscribe((l) => this.lang.set(l));
  }

  readonly option = computed<EChartsOption>(() => {
    this.lang();
    const byMode = new Map(this.modeStats().map((m) => [m.mode, m]));
    const ordered = MODE_ORDER.map((mode) => byMode.get(mode));
    const dealLabel = this.transloco.translate('highlights.radar.dealWinRate');
    const announceLabel = this.transloco.translate('highlights.radar.announceWinRate');

    return {
      tooltip: { trigger: 'item', ...CHART_TOOLTIP },
      legend: {
        bottom: 0,
        textStyle: { color: CHART.text, fontSize: 11 },
        itemWidth: 14,
        itemHeight: 8,
      },
      radar: {
        indicator: MODE_ORDER.map((mode) => ({
          name: `${MODE_SYMBOLS[mode]} ${this.transloco.translate(MODE_LABEL_KEYS[mode])}`,
          max: 100,
        })),
        center: ['50%', '47%'],
        radius: '62%',
        splitNumber: 4,
        axisName: { color: CHART.text, fontSize: 11 },
        axisLine: { lineStyle: { color: CHART.grid } },
        splitLine: { lineStyle: { color: CHART.grid } },
        splitArea: { show: false },
      },
      series: [
        {
          type: 'radar',
          data: [
            {
              name: dealLabel,
              value: ordered.map((m) => m?.dealWinRate ?? 0),
              lineStyle: { width: 2, color: CHART.series1 },
              itemStyle: { color: CHART.series1 },
              areaStyle: { color: CHART.series1, opacity: 0.18 },
              symbolSize: 5,
            },
            {
              name: announceLabel,
              value: ordered.map((m) => m?.announceWinRate ?? 0),
              lineStyle: { width: 2, color: CHART.series2 },
              itemStyle: { color: CHART.series2 },
              symbolSize: 5,
            },
          ],
        },
      ],
    };
  });
}

@Component({
  selector: 'hl-trend',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, EchartComponent],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="TrendingUpIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.trend.title') }}</span>
      </div>
      <div class="chart-box"><app-echart [option]="option()" /></div>
    </section>
  `,
  styles: [CARD_STYLES + `.chart-box { height: 320px; }`],
})
export class HlTrendComponent {
  readonly points = input.required<HighlightsEloPoint[]>();
  readonly TrendingUpIcon = TrendingUp;

  private readonly transloco = inject(TranslocoService);
  private readonly lang = signal('');

  constructor() {
    this.transloco.langChanges$.pipe(takeUntilDestroyed()).subscribe((l) => this.lang.set(l));
  }

  // One x slot per rated match rather than a time axis: play comes in bursts
  // (many matches one evening, nothing for days), which a time axis squashes
  // into vertical zigzags separated by flat gaps.
  readonly option = computed<EChartsOption>(() => {
    const lang = this.lang() || 'en';
    const points = this.points();
    const dates = points.map((p) => new Date(p.recordedAt));
    const axisFmt = new Intl.DateTimeFormat(lang, { month: 'short', day: 'numeric' });
    const tipFmt = new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'short' });
    const elos = points.map((p) => p.elo);
    const min = Math.min(...elos);
    const max = Math.max(...elos);
    const pad = Math.max(10, Math.round((max - min) * 0.15));

    // Label only where the calendar day changes, and never two labels too close.
    const minGap = Math.max(1, Math.floor(points.length / 6));
    let lastLabelIdx = -Infinity;
    const labels = dates.map((d, i) => {
      const prev = dates[i - 1];
      const newDay = !prev || prev.toDateString() !== d.toDateString();
      if (newDay && i - lastLabelIdx >= minGap) {
        lastLabelIdx = i;
        return axisFmt.format(d);
      }
      return '';
    });

    return {
      tooltip: {
        trigger: 'axis',
        ...CHART_TOOLTIP,
        axisPointer: { type: 'line', lineStyle: { color: CHART.grid } },
        formatter: (params: { dataIndex: number }[]) => {
          const i = params[0].dataIndex;
          const elo = elos[i];
          const delta = i > 0 ? elo - elos[i - 1] : 0;
          const sign = delta > 0 ? '+' : '';
          const deltaColor = delta >= 0 ? '#3CA463' : '#D9534F';
          return (
            `<div style="color:${CHART.text};font-size:11px">${tipFmt.format(dates[i])}</div>` +
            `<div><b>${elo}</b> <span style="color:${deltaColor}">${sign}${delta}</span></div>`
          );
        },
      },
      grid: { left: 48, right: 16, top: 16, bottom: 32 },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: labels,
        axisTick: { show: false },
        axisLabel: { color: CHART.text, fontSize: 11, interval: 0, showMinLabel: true },
        axisLine: { lineStyle: { color: CHART.grid } },
        splitLine: { show: false },
      },
      yAxis: {
        type: 'value',
        min: Math.floor((min - pad) / 10) * 10,
        max: Math.ceil((max + pad) / 10) * 10,
        axisLabel: { color: CHART.text, fontSize: 11 },
        splitLine: { lineStyle: { color: CHART.grid } },
      },
      series: [
        {
          type: 'line',
          name: 'Elo',
          showSymbol: false,
          symbolSize: 6,
          lineStyle: { width: 2, color: CHART.series1 },
          itemStyle: { color: CHART.series1 },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(184, 134, 11, 0.22)' },
              { offset: 1, color: 'rgba(184, 134, 11, 0)' },
            ]),
          },
          data: elos,
        },
      ],
    };
  });
}

@Component({
  selector: 'hl-activity',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, EchartComponent],
  template: `
    <section class="card" *transloco="let t">
      <div class="card-head">
        <i-lucide [img]="CalendarDaysIcon" [size]="14"></i-lucide>
        <span>{{ t('highlights.activity.title') }}</span>
      </div>
      <div class="chart-scroll">
        <div class="chart-box"><app-echart [option]="option()" /></div>
      </div>
    </section>
  `,
  styles: [
    CARD_STYLES +
      `.chart-scroll { overflow-x: auto; } .chart-box { height: 170px; min-width: 720px; }`,
  ],
})
export class HlActivityComponent {
  readonly days = input.required<HighlightsActivityDay[]>();
  readonly CalendarDaysIcon = CalendarDays;

  private readonly transloco = inject(TranslocoService);
  private readonly lang = signal('');

  constructor() {
    this.transloco.langChanges$.pipe(takeUntilDestroyed()).subscribe((l) => this.lang.set(l));
  }

  readonly option = computed<EChartsOption>(() => {
    this.lang();
    const end = new Date();
    const start = new Date(end);
    start.setDate(start.getDate() - 364);
    const toIso = (d: Date) => d.toISOString().slice(0, 10);
    const maxCount = Math.max(1, ...this.days().map((d) => d.count));
    const gamesLabel = this.transloco.translate('highlights.activity.games');

    return {
      tooltip: {
        ...CHART_TOOLTIP,
        formatter: (p: { value: [string, number] }) =>
          `${p.value[0]}<br/>${p.value[1]} ${gamesLabel}`,
      },
      visualMap: {
        show: false,
        min: 0,
        max: maxCount,
        inRange: { color: [...CHART.ramp] },
      },
      calendar: {
        top: 28,
        left: 34,
        right: 8,
        bottom: 8,
        range: [toIso(start), toIso(end)],
        cellSize: ['auto', 13],
        splitLine: { show: false },
        itemStyle: {
          color: CHART.emptyCell,
          borderColor: 'hsl(220, 20%, 14%)',
          borderWidth: 2,
        },
        dayLabel: { color: CHART.text, fontSize: 10, firstDay: 1 },
        monthLabel: { color: CHART.text, fontSize: 10 },
        yearLabel: { show: false },
      },
      series: [
        {
          type: 'heatmap',
          coordinateSystem: 'calendar',
          data: this.days().map((d) => [d.date, d.count]),
        },
      ],
    };
  });
}
