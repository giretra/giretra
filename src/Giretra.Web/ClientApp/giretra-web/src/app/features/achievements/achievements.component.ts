import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import {
  ApiService,
  AchievementShowcaseResponse,
  AchievementShowcaseItem,
} from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { FormsModule } from '@angular/forms';
import { TagModule } from 'primeng/tag';
import { AvatarModule } from 'primeng/avatar';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ProgressBarModule } from 'primeng/progressbar';
import { SkeletonModule } from 'primeng/skeleton';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

type SortMode = 'rarity' | 'name' | 'recent';

@Component({
  selector: 'app-achievements',
  standalone: true,
  imports: [
    FormsModule,
    TranslocoDirective,
    TagModule,
    AvatarModule,
    SelectButtonModule,
    ProgressBarModule,
    SkeletonModule,
  ],
  template: `
    <div class="ach" *transloco="let t">
      @if (loading()) {
        <div class="grid grid-cols-12 gap-6">
          <div class="col-span-12 xl:col-span-8"><p-skeleton height="11rem" borderRadius="24px" /></div>
          <div class="col-span-12 xl:col-span-4"><p-skeleton height="11rem" borderRadius="24px" /></div>
          <div class="col-span-12"><p-skeleton height="20rem" borderRadius="24px" /></div>
        </div>
      } @else if (showcase(); as s) {
        <div class="grid grid-cols-12 gap-6">
          <!-- Progress hero -->
          <div class="col-span-12 xl:col-span-8">
            <section class="g-card hero">
              <div class="g-card-header">
                <div class="hero-who">
                  @if (isOtherPlayer()) {
                    <p-avatar [label]="playerInitial()" shape="circle" size="large" styleClass="hero-avatar" />
                  } @else {
                    <span class="hero-badge"><i class="pi pi-star-fill"></i></span>
                  }
                  <div>
                    <div class="g-card-title">
                      @if (isOtherPlayer()) { {{ s.playerName }} } @else { {{ t('achievements.page.title') }} }
                    </div>
                    <p class="g-card-subtitle">{{ t('achievements.page.subtitle', { earned: s.earnedCount, total: s.totalCount }) }}</p>
                  </div>
                </div>
                <span class="hero-percent">{{ progressPercent() }}<small>%</small></span>
              </div>
              <p-progressbar [value]="progressPercent()" [showValue]="false" color="var(--p-yellow-400)" styleClass="hero-bar" />
              <div class="hero-stats">
                <span class="stat earned"><i class="pi pi-check-circle"></i>{{ s.earnedCount }} {{ t('achievements.page.earned') }}</span>
                <span class="stat"><i class="pi pi-lock"></i>{{ s.totalCount - s.earnedCount }} {{ t('achievements.page.locked') }}</span>
              </div>
            </section>
          </div>

          <!-- Latest unlock / how to earn -->
          <div class="col-span-12 xl:col-span-4">
            <section class="g-card side">
              @if (latestUnlock(); as latest) {
                <div class="latest">
                  <span class="latest-label">{{ t('achievements.page.latestUnlock') }}</span>
                  <div class="latest-row">
                    <span class="ach-icon earned"><i class="pi pi-star-fill"></i></span>
                    <div class="latest-info">
                      <span class="latest-name">{{ latest.name }}</span>
                      <span class="latest-date">{{ formatDate(latest.earnedAt!) }}</span>
                    </div>
                  </div>
                </div>
                <div class="g-divider"></div>
              }
              <div class="info">
                <i class="pi pi-info-circle"></i>
                <p>
                  @if (qualifyingBotsLabel(); as bots) {
                    {{ t('achievements.page.infoBots', { bots }) }}
                  } @else {
                    {{ t('achievements.page.info') }}
                  }
                </p>
              </div>
            </section>
          </div>

          <!-- Achievement list -->
          <div class="col-span-12">
            <section class="g-card">
              <div class="g-card-header list-head">
                <div>
                  <div class="g-card-title">
                    {{ t('achievements.page.title') }}
                    <p-tag [value]="s.totalCount.toString()" severity="secondary" [rounded]="true" />
                  </div>
                </div>
                <p-selectbutton
                  [options]="sortChoices()"
                  optionLabel="label"
                  optionValue="value"
                  [ngModel]="sortBy()"
                  (ngModelChange)="sortBy.set($event)"
                  [allowEmpty]="false"
                  size="small"
                  [attr.aria-label]="t('achievements.page.sortBy')"
                />
              </div>

              @if (s.achievements.length === 0) {
                <div class="g-empty">
                  <span class="g-empty-icon"><i class="pi pi-star"></i></span>
                  <span class="g-empty-title">{{ t('achievements.page.noAchievements') }}</span>
                </div>
              }

              @if (earnedAchievements().length > 0) {
                <div class="section-head earned">
                  <i class="pi pi-check-circle"></i>
                  <span>{{ t('achievements.page.earned') }}</span>
                  <span class="section-count">{{ earnedAchievements().length }}</span>
                </div>
                <div class="tier-grid">
                  @for (ach of earnedAchievements(); track ach.code) {
                    <article class="ach-card earned" [class.tier-high]="ach.tier >= 4">
                      <div class="ach-top">
                        <span class="ach-icon earned"><i class="pi pi-star-fill"></i></span>
                        <div class="ach-info">
                          <span class="ach-name">{{ ach.name }}</span>
                          <span class="ach-stars">
                            @for (_ of starArray(ach.tier); track $index) { <i class="pi pi-star-fill"></i> }
                            @for (_ of starArray(5 - ach.tier); track $index) { <i class="pi pi-star"></i> }
                          </span>
                        </div>
                        <p-tag [value]="t('achievements.page.earned')" severity="warn" icon="pi pi-check" [rounded]="true" />
                      </div>
                      <p class="ach-desc">{{ t('achievements.desc.' + ach.code) }}</p>
                      <div class="ach-meta">
                        <p-tag [value]="t('achievements.page.category.' + ach.category)" severity="secondary" />
                        @if (ach.earnedAt) {
                          <span class="ach-date">{{ t('achievements.page.earnedOn', { date: formatDate(ach.earnedAt) }) }}</span>
                        }
                      </div>
                    </article>
                  }
                </div>
              }

              @if (lockedAchievements().length > 0) {
                <div class="section-head">
                  <i class="pi pi-lock"></i>
                  <span>{{ t('achievements.page.locked') }}</span>
                  <span class="section-count">{{ lockedAchievements().length }}</span>
                </div>
                <div class="tier-grid">
                  @for (ach of lockedAchievements(); track ach.code) {
                    <article class="ach-card locked" [class.hidden-ach]="ach.isHidden">
                      <div class="ach-top">
                        <span class="ach-icon"><i class="pi pi-lock"></i></span>
                        <div class="ach-info">
                          <span class="ach-name" [class.hidden-text]="ach.isHidden">{{ ach.isHidden ? '???' : ach.name }}</span>
                          <span class="ach-stars">
                            @for (_ of starArray(ach.tier); track $index) { <i class="pi pi-star-fill"></i> }
                            @for (_ of starArray(5 - ach.tier); track $index) { <i class="pi pi-star"></i> }
                          </span>
                        </div>
                      </div>
                      <p class="ach-desc" [class.hidden-text]="ach.isHidden">
                        {{ ach.isHidden ? t('achievements.page.hiddenDesc') : t('achievements.desc.' + ach.code) }}
                      </p>
                      <div class="ach-meta">
                        <p-tag [value]="t('achievements.page.category.' + ach.category)" severity="secondary" />
                      </div>
                    </article>
                  }
                </div>
              }
            </section>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .hero .g-card-header { align-items:center; padding-bottom:1.25rem; }
    .hero-who { display:flex; align-items:center; gap:0.875rem; min-width:0; }
    .hero-badge { display:inline-flex; align-items:center; justify-content:center; width:3rem; height:3rem; border-radius:0.875rem; background:color-mix(in srgb, var(--p-yellow-400) 18%, transparent); color:var(--p-yellow-400); flex-shrink:0; }
    .hero-badge i { font-size:1.25rem; }
    :host ::ng-deep .hero-avatar { background:color-mix(in srgb, var(--p-yellow-400) 18%, transparent); color:var(--p-yellow-400); font-weight:700; }
    .hero-percent { font-size:2.25rem; font-weight:800; line-height:1; color:var(--p-yellow-400); font-variant-numeric:tabular-nums; }
    .hero-percent small { font-size:1rem; font-weight:700; opacity:0.7; margin-left:0.125rem; }
    :host ::ng-deep .hero-bar { height:0.625rem; border-radius:9999px; background:var(--p-surface-800); }
    :host ::ng-deep .hero-bar .p-progressbar-value { border-radius:9999px; }
    .hero-stats { display:flex; justify-content:space-between; gap:1rem; margin-top:0.875rem; font-size:0.8125rem; color:var(--text-color-secondary); }
    .stat { display:inline-flex; align-items:center; gap:0.375rem; }
    .stat.earned { color:var(--p-yellow-400); font-weight:500; }

    .side { display:flex; flex-direction:column; gap:1rem; height:100%; }
    .latest { display:flex; flex-direction:column; gap:0.625rem; }
    .latest-label { font-size:0.6875rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; color:var(--text-color-secondary); }
    .latest-row { display:flex; align-items:center; gap:0.75rem; }
    .latest-info { display:flex; flex-direction:column; min-width:0; }
    .latest-name { font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .latest-date { font-size:0.75rem; color:var(--text-color-secondary); }
    .info { display:flex; gap:0.625rem; font-size:0.8125rem; color:var(--text-color-secondary); line-height:1.5; }
    .info i { margin-top:0.125rem; flex-shrink:0; }
    .info p { margin:0; }

    .list-head { align-items:center; flex-wrap:wrap; }
    .section-head { display:flex; align-items:center; gap:0.5rem; margin:0.5rem 0 0.75rem; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; color:var(--text-color-secondary); }
    .section-head.earned { color:var(--p-yellow-400); }
    .section-count { margin-left:auto; font-variant-numeric:tabular-nums; }
    .tier-grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(17rem, 1fr)); gap:0.75rem; margin-bottom:1rem; }
    .tier-grid:last-child { margin-bottom:0; }

    .ach-card { display:flex; flex-direction:column; gap:0.625rem; padding:1rem; border-radius:1rem; border:1px solid var(--surface-border); background:var(--p-surface-900); }
    .ach-card.earned { border-color:color-mix(in srgb, var(--p-yellow-400) 30%, transparent); background:color-mix(in srgb, var(--p-yellow-400) 5%, var(--p-surface-900)); }
    .ach-card.earned.tier-high { border-color:color-mix(in srgb, var(--p-yellow-400) 55%, transparent); }
    .ach-card.locked { opacity:0.7; }
    .ach-card.hidden-ach { border-style:dashed; }
    .ach-top { display:flex; align-items:center; gap:0.75rem; }
    .ach-icon { display:inline-flex; align-items:center; justify-content:center; width:2.5rem; height:2.5rem; border-radius:0.75rem; background:var(--p-surface-800); color:var(--text-color-secondary); flex-shrink:0; }
    .ach-icon.earned { background:color-mix(in srgb, var(--p-yellow-400) 18%, transparent); color:var(--p-yellow-400); }
    .ach-info { flex:1; min-width:0; display:flex; flex-direction:column; gap:0.25rem; }
    .ach-name { font-weight:600; line-height:1.25; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
    .ach-stars { display:flex; gap:0.125rem; font-size:0.625rem; color:var(--p-yellow-400); }
    .ach-stars .pi-star { color:var(--p-surface-600); }
    .hidden-text { color:var(--text-color-secondary); font-style:italic; }
    .ach-desc { margin:0; font-size:0.875rem; line-height:1.5; color:var(--text-color-secondary); flex:1; }
    .ach-meta { display:flex; align-items:center; justify-content:space-between; gap:0.5rem; padding-top:0.625rem; border-top:1px solid var(--surface-border); }
    .ach-date { font-size:0.75rem; color:var(--p-yellow-400); }
  `],
})
export class AchievementsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);


  readonly sortOptions: SortMode[] = ['rarity', 'name', 'recent'];

  private readonly lang = toSignal(this.transloco.langChanges$);

  readonly sortChoices = computed(() => {
    this.lang();
    return this.sortOptions.map((value) => ({
      value,
      label: this.transloco.translate('achievements.page.sort.' + value),
    }));
  });

  readonly loading = signal(true);
  readonly showcase = signal<AchievementShowcaseResponse | null>(null);
  readonly sortBy = signal<SortMode>('rarity');
  private playerId: string | null = null;

  readonly isOtherPlayer = computed(() => !!this.playerId);

  readonly playerInitial = computed(() => {
    const name = this.showcase()?.playerName;
    return name ? name.charAt(0).toUpperCase() : '?';
  });

  readonly progressPercent = computed(() => {
    const s = this.showcase();
    if (!s || s.totalCount === 0) return 0;
    return Math.round((s.earnedCount / s.totalCount) * 100);
  });

  readonly qualifyingBotsLabel = computed(() => {
    const bots = this.showcase()?.qualifyingBots;
    return bots && bots.length > 0 ? bots.join(' & ') : null;
  });

  readonly latestUnlock = computed(() => {
    const s = this.showcase();
    if (!s) return null;
    let latest: AchievementShowcaseItem | null = null;
    for (const a of s.achievements) {
      if (!a.isEarned || !a.earnedAt) continue;
      if (!latest || Date.parse(a.earnedAt) > Date.parse(latest.earnedAt!)) latest = a;
    }
    return latest;
  });

  readonly earnedAchievements = computed(() =>
    this.sortItems((this.showcase()?.achievements ?? []).filter(a => a.isEarned)));

  readonly lockedAchievements = computed(() =>
    this.sortItems((this.showcase()?.achievements ?? []).filter(a => !a.isEarned)));

  private sortItems(items: AchievementShowcaseItem[]): AchievementShowcaseItem[] {
    const mode = this.sortBy();
    return [...items].sort((a, b) => {
      if (mode === 'name') return a.name.localeCompare(b.name);
      if (mode === 'recent') {
        const da = a.earnedAt ? Date.parse(a.earnedAt) : 0;
        const db = b.earnedAt ? Date.parse(b.earnedAt) : 0;
        if (db !== da) return db - da;
      }
      return b.tier - a.tier || a.name.localeCompare(b.name);
    });
  }

  ngOnInit(): void {
    this.playerId = this.route.snapshot.paramMap.get('playerId') || null;

    if (this.playerId) {
      this.api.getPlayerAchievementShowcase(this.playerId).subscribe({
        next: (data) => {
          this.showcase.set(data);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      this.api.getMyAchievementShowcase().subscribe({
        next: (data) => {
          this.showcase.set(data);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  starArray(n: number): number[] {
    return Array.from({ length: Math.max(0, n) }, (_, i) => i);
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString(this.transloco.getActiveLang(), {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  }

}
