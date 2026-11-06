import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService, AdminClipEntry, ClipStatus } from '../../../core/services/api.service';
import { LucideAngularModule, Video, RotateCw, Trash2, ExternalLink } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

const FILTERS: (ClipStatus | null)[] = [null, 'Failed', 'Pending', 'Rendering', 'Ready', 'Evicted'];

/** Moderation view of shared clips: render diagnostics, retry, takedown. */
@Component({
  selector: 'app-admin-clips',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective, DatePipe],
  template: `
    <div class="ac-inner" *transloco="let t">
      <div class="page-head">
        <h1 class="header-title">
          <i-lucide [img]="VideoIcon" [size]="18"></i-lucide>
          {{ t('adminClips.title') }}
        </h1>
        @if (totalCount() > 0) {
          <span class="count-badge">{{ t('adminClips.count', { count: totalCount() }) }}</span>
        }
      </div>

      <div class="filters">
        @for (f of filters; track f) {
          <button type="button" class="chip" [class.active]="filter() === f" (click)="setFilter(f)">
            {{ f ? t('clip.status.' + f) : t('adminClips.all') }}
          </button>
        }
      </div>

      @if (loading()) {
        <div class="loading-state">{{ t('common.loading') }}</div>
      } @else if (items().length === 0) {
        <div class="empty-state">{{ t('adminClips.empty') }}</div>
      } @else {
        <div class="table-panel">
          <div class="row row-header">
            <div class="col-title">{{ t('adminClips.columns.clip') }}</div>
            <div class="col-by">{{ t('adminClips.columns.createdBy') }}</div>
            <div class="col-status">{{ t('adminClips.columns.status') }}</div>
            <div class="col-error">{{ t('adminClips.columns.error') }}</div>
            <div class="col-views">{{ t('adminClips.columns.views') }}</div>
            <div class="col-date">{{ t('adminClips.columns.created') }}</div>
            <div class="col-actions"></div>
          </div>

          @for (e of items(); track e.clip.shortId) {
            <div class="row">
              <div class="col-title">
                <span class="clip-title" [title]="e.clip.title">{{ e.clip.title }}</span>
                <span class="clip-id">{{ e.clip.shortId }} · {{ e.clip.locale }}</span>
              </div>
              <div class="col-by">{{ e.createdByName }}</div>
              <div class="col-status">
                <span class="flag" [class]="'flag flag-' + e.clip.status.toLowerCase()">{{
                  t('clip.status.' + e.clip.status)
                }}</span>
                @if (e.attempts > 0) {
                  <span class="attempts">{{
                    t('adminClips.attempts', { count: e.attempts })
                  }}</span>
                }
              </div>
              <div class="col-error" [title]="e.error ?? ''">{{ e.error ?? '–' }}</div>
              <div class="col-views">{{ e.clip.viewCount }}</div>
              <div class="col-date" [title]="e.clip.createdAt">
                {{ e.clip.createdAt | date: 'MMM d, y HH:mm' }}
              </div>
              <div class="col-actions">
                <a
                  class="icon-btn"
                  [href]="e.clip.pageUrl"
                  target="_blank"
                  rel="noopener"
                  [title]="t('adminClips.open')"
                >
                  <i-lucide [img]="ExternalLinkIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </a>
                <button
                  type="button"
                  class="icon-btn"
                  [disabled]="busy() === e.clip.shortId || e.clip.status === 'Rendering'"
                  (click)="retry(e)"
                  [title]="t('adminClips.retry')"
                >
                  <i-lucide [img]="RotateCwIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
                <button
                  type="button"
                  class="icon-btn danger"
                  [disabled]="busy() === e.clip.shortId"
                  (click)="remove(e)"
                  [title]="t('adminClips.remove')"
                >
                  <i-lucide [img]="Trash2Icon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
              </div>
            </div>
          }
        </div>

        @if (totalPages() > 1) {
          <div class="pagination">
            <button class="page-btn" [disabled]="page() <= 1" (click)="setPage(page() - 1)">
              {{ t('adminUsers.prev') }}
            </button>
            <span class="page-info">{{
              t('adminUsers.pageInfo', { page: page(), totalPages: totalPages() })
            }}</span>
            <button
              class="page-btn"
              [disabled]="page() >= totalPages()"
              (click)="setPage(page() + 1)"
            >
              {{ t('adminUsers.next') }}
            </button>
          </div>
        }
      }
    </div>
  `,
  styles: [
    `
      .header-title {
        margin: 0;
        font-size: 1.125rem;
        font-weight: 700;
        color: hsl(var(--foreground));
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .count-badge {
        margin-left: auto;
        font-size: 0.6875rem;
        font-weight: 600;
        color: hsl(var(--muted-foreground));
        background: hsl(var(--muted) / 0.5);
        padding: 0.125rem 0.625rem;
        border-radius: 9999px;
      }
      .page-head {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        flex-wrap: wrap;
        margin-bottom: 1rem;
      }
      .filters {
        display: flex;
        gap: 0.375rem;
        flex-wrap: wrap;
        margin-bottom: 0.75rem;
      }
      .chip {
        font-size: 0.6875rem;
        font-weight: 600;
        padding: 0.25rem 0.75rem;
        border-radius: 9999px;
        border: 1px solid hsl(var(--border));
        background: hsl(var(--card));
        color: hsl(var(--muted-foreground));
        cursor: pointer;
      }
      .chip.active {
        color: hsl(var(--gold));
        border-color: hsl(var(--gold) / 0.5);
        background: hsl(var(--gold) / 0.12);
      }
      .loading-state,
      .empty-state {
        text-align: center;
        padding: 3rem 1rem;
        color: hsl(var(--muted-foreground));
        font-size: 0.875rem;
      }
      .table-panel {
        background: hsl(var(--card));
        border: 1px solid hsl(var(--border));
        border-radius: 0.75rem;
        padding: 0.75rem 1rem;
        overflow-x: auto;
      }
      .row {
        display: flex;
        align-items: center;
        padding: 0.5rem 0.25rem;
        gap: 0.625rem;
        min-width: 60rem;
      }
      .row:not(.row-header) {
        border-top: 1px solid hsl(var(--border) / 0.5);
      }
      .row-header {
        font-size: 0.625rem;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        color: hsl(var(--muted-foreground));
      }
      .col-title {
        flex: 1;
        min-width: 14rem;
        display: flex;
        flex-direction: column;
        gap: 0.125rem;
        overflow: hidden;
      }
      .clip-title {
        font-size: 0.8125rem;
        font-weight: 600;
        color: hsl(var(--foreground));
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .clip-id {
        font-size: 0.6875rem;
        color: hsl(var(--muted-foreground));
        font-family: ui-monospace, monospace;
      }
      .col-by {
        width: 8rem;
        flex-shrink: 0;
        font-size: 0.75rem;
        color: hsl(var(--muted-foreground));
        overflow: hidden;
        white-space: nowrap;
        text-overflow: ellipsis;
      }
      .col-status {
        width: 9rem;
        flex-shrink: 0;
        display: flex;
        align-items: center;
        gap: 0.375rem;
      }
      .attempts {
        font-size: 0.625rem;
        color: hsl(var(--muted-foreground));
      }
      .col-error {
        width: 14rem;
        flex-shrink: 0;
        font-size: 0.6875rem;
        color: hsl(0 70% 60%);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        font-family: ui-monospace, monospace;
      }
      .col-views {
        width: 3.5rem;
        flex-shrink: 0;
        text-align: right;
        font-size: 0.75rem;
        color: hsl(var(--muted-foreground));
        font-variant-numeric: tabular-nums;
      }
      .col-date {
        width: 8.5rem;
        flex-shrink: 0;
        text-align: right;
        font-size: 0.75rem;
        color: hsl(var(--muted-foreground));
        white-space: nowrap;
      }
      .col-actions {
        width: 6rem;
        flex-shrink: 0;
        display: flex;
        gap: 0.25rem;
        justify-content: flex-end;
      }
      .icon-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 1.75rem;
        height: 1.75rem;
        border-radius: 0.5rem;
        border: 1px solid hsl(var(--border));
        background: transparent;
        color: hsl(var(--muted-foreground));
        cursor: pointer;
      }
      .icon-btn:hover:not(:disabled) {
        color: hsl(var(--foreground));
        border-color: hsl(var(--foreground) / 0.3);
      }
      .icon-btn.danger:hover:not(:disabled) {
        color: hsl(0 70% 60%);
        border-color: hsl(0 70% 50% / 0.4);
      }
      .icon-btn:disabled {
        opacity: 0.35;
        cursor: default;
      }
      .flag {
        font-size: 0.5625rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        padding: 0.0625rem 0.4375rem;
        border-radius: 9999px;
        color: hsl(var(--muted-foreground));
        background: hsl(var(--muted) / 0.5);
      }
      .flag-ready {
        color: hsl(140 60% 45%);
        background: hsl(140 60% 45% / 0.12);
      }
      .flag-failed {
        color: hsl(0 70% 55%);
        background: hsl(0 70% 50% / 0.12);
      }
      .flag-rendering,
      .flag-pending {
        color: hsl(var(--gold));
        background: hsl(var(--gold) / 0.12);
      }
      .pagination {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 1rem;
        margin-top: 1rem;
      }
      .page-info {
        font-size: 0.75rem;
        color: hsl(var(--muted-foreground));
      }
      .page-btn {
        font-size: 0.75rem;
        font-weight: 600;
        color: hsl(var(--foreground));
        background: hsl(var(--card));
        border: 1px solid hsl(var(--border));
        border-radius: 0.5rem;
        padding: 0.375rem 0.875rem;
        cursor: pointer;
      }
      .page-btn:disabled {
        opacity: 0.4;
        cursor: default;
      }
    `,
  ],
})
export class AdminClipsComponent implements OnInit {
  readonly VideoIcon = Video;
  readonly RotateCwIcon = RotateCw;
  readonly Trash2Icon = Trash2;
  readonly ExternalLinkIcon = ExternalLink;

  private readonly api = inject(ApiService);
  private readonly transloco = inject(TranslocoService);

  readonly filters = FILTERS;
  readonly filter = signal<ClipStatus | null>('Failed');
  readonly items = signal<AdminClipEntry[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 25;
  readonly loading = signal(true);
  readonly busy = signal<string | null>(null);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  ngOnInit(): void {
    this.load();
  }

  setFilter(status: ClipStatus | null): void {
    this.filter.set(status);
    this.page.set(1);
    this.load();
  }

  setPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  retry(entry: AdminClipEntry): void {
    this.busy.set(entry.clip.shortId);
    this.api.retryAdminClip(entry.clip.shortId).subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
      },
      error: () => this.busy.set(null),
    });
  }

  remove(entry: AdminClipEntry): void {
    if (!confirm(this.transloco.translate('adminClips.removeConfirm'))) return;
    this.busy.set(entry.clip.shortId);
    this.api.deleteAdminClip(entry.clip.shortId).subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
      },
      error: () => this.busy.set(null),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAdminClips(this.filter(), this.page(), this.pageSize).subscribe({
      next: (res) => {
        this.items.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
