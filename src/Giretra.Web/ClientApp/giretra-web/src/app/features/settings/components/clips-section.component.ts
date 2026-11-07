import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { ButtonModule } from 'primeng/button';
import { ApiService, ClipResponse, ClipStatus } from '../../../core/services/api.service';
import { ErrorBannerService } from '../../../core/services/error-banner.service';

@Component({
  selector: 'app-clips-section',
  standalone: true,
  imports: [DatePipe, TranslocoDirective, TagModule, SkeletonModule, ButtonModule],
  template: `
    <ng-container *transloco="let t">
      <div class="g-panel-head">
        <div class="g-panel-text">
          <span class="g-panel-title">{{ t('settings.clips.title') }}</span>
          <span class="g-form-desc">{{ t('settings.clips.hint') }}</span>
        </div>
      </div>
      <div class="g-divider mx"></div>
      <div class="g-panel-body">
        @if (loading()) {
          @for (i of [0, 1, 2]; track i) {
            <p-skeleton height="4.5rem" borderRadius="16px" />
          }
        } @else if (clips().length === 0) {
          <div class="g-empty compact">
            <span class="g-empty-icon"><i class="pi pi-video"></i></span>
            <span class="g-empty-hint">{{ t('settings.clips.empty') }}</span>
          </div>
        }
        @for (clip of clips(); track clip.shortId) {
          <article class="clip">
            @if (clip.posterUrl) {
              <img class="thumb" [src]="clip.posterUrl" alt="" />
            } @else {
              <div class="thumb placeholder"><i class="pi pi-video"></i></div>
            }
            <div class="clip-main">
              <span class="clip-title">{{ clip.title }}</span>
              <span class="clip-meta">
                {{ clip.createdAt | date: 'mediumDate' }} ·
                {{ t('settings.clips.views', { count: clip.viewCount }) }}
              </span>
              <div class="clip-actions">
                <p-tag
                  [value]="t('clip.status.' + clip.status)"
                  [severity]="severity(clip.status)"
                  [rounded]="true"
                />
                <a class="action" [href]="clip.pageUrl" target="_blank" rel="noopener"
                  ><i class="pi pi-external-link"></i> {{ t('settings.clips.open') }}</a
                >
                <button type="button" class="action" (click)="copy(clip)">
                  <i class="pi pi-copy"></i> {{ t('settings.clips.copy') }}
                </button>
                <button type="button" class="action danger" (click)="remove(clip)">
                  <i class="pi pi-trash"></i> {{ t('settings.clips.delete') }}
                </button>
              </div>
            </div>
          </article>
        }
      </div>
    </ng-container>
  `,
  styles: [
    `
      :host {
        display: flex;
        flex-direction: column;
        flex: 1;
      }
      .g-panel-text {
        display: flex;
        flex-direction: column;
        gap: 0.125rem;
      }
      .g-divider.mx {
        margin: 0 1.5rem;
      }
      .g-panel-body {
        gap: 0.5rem;
      }
      .g-empty.compact {
        padding: 1.5rem 0.5rem;
      }
      .clip {
        display: flex;
        gap: 0.875rem;
        padding: 0.75rem 1rem;
        border: 1px solid var(--surface-border);
        border-radius: 1rem;
        background: var(--p-surface-900);
      }
      .thumb {
        width: 3.5rem;
        height: 6.2rem;
        object-fit: cover;
        border-radius: 0.5rem;
        background: #000;
        flex-shrink: 0;
      }
      .thumb.placeholder {
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--p-text-muted-color);
        background: var(--p-surface-800);
      }
      .clip-main {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        min-width: 0;
      }
      .clip-title {
        font-weight: 600;
      }
      .clip-meta {
        font-size: 0.8rem;
        color: var(--p-text-muted-color);
      }
      .clip-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem 0.875rem;
        align-items: center;
        margin-top: 0.25rem;
      }
      .action {
        display: inline-flex;
        align-items: center;
        gap: 0.3rem;
        background: none;
        border: none;
        padding: 0;
        color: var(--p-primary-color);
        font: inherit;
        font-size: 0.85rem;
        cursor: pointer;
        text-decoration: none;
      }
      .action.danger {
        color: var(--p-red-400);
      }
      .action:hover {
        text-decoration: underline;
      }
    `,
  ],
})
export class ClipsSectionComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly errorBanner = inject(ErrorBannerService);

  readonly loading = signal(true);
  readonly clips = signal<ClipResponse[]>([]);

  ngOnInit(): void {
    this.api.getMyClips().subscribe({
      next: (list) => {
        this.clips.set(list.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  severity(status: ClipStatus): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'Ready':
        return 'success';
      case 'Failed':
        return 'danger';
      case 'Pending':
      case 'Rendering':
        return 'info';
      default:
        return 'secondary';
    }
  }

  async copy(clip: ClipResponse): Promise<void> {
    try {
      await navigator.clipboard.writeText(clip.pageUrl);
      this.errorBanner.show(this.transloco.translate('clip.share.copied'));
    } catch {
      this.errorBanner.show(this.transloco.translate('home.linkCopyFailed'));
    }
  }

  remove(clip: ClipResponse): void {
    if (!confirm(this.transloco.translate('settings.clips.deleteConfirm'))) return;
    this.api.deleteClip(clip.shortId).subscribe({
      next: () => this.clips.update((list) => list.filter((c) => c.shortId !== clip.shortId)),
    });
  }
}
