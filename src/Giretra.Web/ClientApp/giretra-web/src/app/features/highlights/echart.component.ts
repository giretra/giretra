import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  viewChild,
} from '@angular/core';
import type { ECharts } from 'echarts/core';
import { echarts, EChartsOption } from './echarts-setup';

@Component({
  selector: 'app-echart',
  standalone: true,
  template: `<div #host class="echart-host"></div>`,
  styles: [
    `
    :host { display: block; width: 100%; height: 100%; }
    .echart-host { width: 100%; height: 100%; }
  `,
  ],
})
export class EchartComponent {
  readonly option = input.required<EChartsOption>();

  private readonly host = viewChild.required<ElementRef<HTMLDivElement>>('host');
  private chart: ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    afterNextRender(() => {
      const el = this.host().nativeElement;
      this.chart = echarts.init(el, null, { renderer: 'canvas' });
      this.chart.setOption(this.option());
      this.resizeObserver = new ResizeObserver(() => this.chart?.resize());
      this.resizeObserver.observe(el);
    });

    effect(() => {
      const opt = this.option();
      this.chart?.setOption(opt, true);
    });

    inject(DestroyRef).onDestroy(() => {
      this.resizeObserver?.disconnect();
      this.chart?.dispose();
      this.chart = null;
    });
  }
}
