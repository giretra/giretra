import { Component, input, computed } from '@angular/core';

@Component({
  selector: 'app-shield-icon',
  standalone: true,
  template: `
    <svg
      [style.height]="size()"
      viewBox="0 0 100 120"
      role="img"
      [attr.aria-label]="ariaLabel()"
    >
      <path
        d="M50 4 L92 20 L92 65 Q92 95 50 116 Q8 95 8 65 L8 20 Z"
        [attr.fill]="color()"
      />
      <text
        x="50"
        y="74"
        text-anchor="middle"
        font-family="Georgia,serif"
        font-weight="700"
        font-size="56"
        fill="#fff"
      >
        {{ letter() }}
      </text>
    </svg>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
        line-height: 1;
      }
    `,
  ],
})
export class ShieldIconComponent {
  readonly type = input.required<'no-trumps' | 'all-trumps'>();
  readonly size = input<string>('1.5rem');

  readonly letter = computed(() => (this.type() === 'no-trumps' ? 'A' : 'J'));
  readonly color = computed(() => (this.type() === 'no-trumps' ? '#E63946' : '#2D864D'));
  readonly ariaLabel = computed(() => (this.type() === 'no-trumps' ? 'No Trumps' : 'All Trumps'));
}
