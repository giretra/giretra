import * as echarts from 'echarts/core';
import type { EChartsCoreOption } from 'echarts/core';
import { RadarChart, LineChart, HeatmapChart } from 'echarts/charts';
import {
  TooltipComponent,
  GridComponent,
  LegendComponent,
  RadarComponent,
  CalendarComponent,
  VisualMapComponent,
} from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

echarts.use([
  RadarChart,
  LineChart,
  HeatmapChart,
  TooltipComponent,
  GridComponent,
  LegendComponent,
  RadarComponent,
  CalendarComponent,
  VisualMapComponent,
  CanvasRenderer,
]);

export { echarts };
export type EChartsOption = EChartsCoreOption;

// Literal colors mirroring the :root design tokens in styles.css — canvas charts
// cannot resolve hsl(var(--x)), and the app is dark-only so static values are safe.
// The series pair is validated for CVD separation and contrast on the card surface.
export const CHART = {
  series1: '#B8860B', // gold family, darkened into the dark-surface lightness band
  series2: '#3C8CDD', // --team1 blue
  text: 'hsl(215, 16%, 57%)', // --muted-foreground
  textStrong: 'hsl(210, 40%, 96%)', // --foreground
  grid: 'hsl(220, 15%, 20%)', // --border
  tooltipBg: 'hsl(220, 20%, 12%)',
  // Sequential single-hue green ramp (dark -> bright) for the activity calendar
  ramp: ['#1E4A30', '#27633F', '#31824F', '#3CA463'],
  emptyCell: 'hsl(220, 15%, 18%)',
} as const;

export const CHART_TOOLTIP = {
  backgroundColor: CHART.tooltipBg,
  borderColor: CHART.grid,
  textStyle: { color: CHART.textStrong, fontSize: 12 },
} as const;
