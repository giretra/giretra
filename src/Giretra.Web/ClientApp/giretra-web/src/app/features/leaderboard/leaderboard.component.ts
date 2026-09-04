import { Component, inject, signal, computed, OnInit } from '@angular/core';
import {
  ApiService,
  LeaderboardPlayerEntry,
  LeaderboardAchieverEntry,
  LeaderboardBotEntry,
  PlayerProfileResponse,
} from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { AvatarModule } from 'primeng/avatar';
import { SkeletonModule } from 'primeng/skeleton';
import { TranslocoDirective } from '@jsverse/transloco';
import { PlayerProfilePopupComponent } from '../../shared/components/player-profile-popup/player-profile-popup.component';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [TranslocoDirective, PlayerProfilePopupComponent, TableModule, TagModule, AvatarModule, SkeletonModule],
  template: `
    <div class="lb" *transloco="let t">
      @if (loading()) {
        <div class="grid grid-cols-12 gap-6">
          @for (i of [0, 1, 2]; track i) {
            <div class="col-span-12 md:col-span-4"><p-skeleton height="6.5rem" borderRadius="24px" /></div>
          }
          @for (i of [0, 1, 2]; track i) {
            <div class="col-span-12 xl:col-span-4"><p-skeleton height="22rem" borderRadius="24px" /></div>
          }
        </div>
      } @else {
        <div class="grid grid-cols-12 gap-6">
          <!-- KPI tiles -->
          <div class="col-span-12 md:col-span-4">
            <section class="g-card tile tile-me">
              <div class="tile-text">
                <span class="tile-label">{{ t('leaderboard.yourRanking') }}</span>
                @if (currentUserEntry(); as me) {
                  <span class="tile-value">#{{ me.rank }}</span>
                  <span class="tile-sub">{{ me.rating }} {{ t('leaderboard.columns.rating') }} · {{ me.winRate }}% {{ t('leaderboard.columns.winRate') }}</span>
                } @else {
                  <span class="tile-value muted">—</span>
                  <span class="tile-sub">{{ t('leaderboard.unrankedNotice') }}</span>
                }
              </div>
              <i class="pi pi-trophy tile-icon"></i>
            </section>
          </div>
          <div class="col-span-6 md:col-span-4">
            <section class="g-card tile">
              <div class="tile-text">
                <span class="tile-label">{{ t('leaderboard.players') }}</span>
                <span class="tile-value">{{ playerCount() }}</span>
              </div>
              <i class="pi pi-users tile-icon"></i>
            </section>
          </div>
          <div class="col-span-6 md:col-span-4">
            <section class="g-card tile">
              <div class="tile-text">
                <span class="tile-label">{{ t('leaderboard.bots') }}</span>
                <span class="tile-value">{{ botCount() }}</span>
              </div>
              <i class="pi pi-microchip tile-icon"></i>
            </section>
          </div>

          <!-- Players -->
          <div class="col-span-12 xl:col-span-4">
            <section class="g-card board">
              <div class="g-card-header">
                <div class="g-card-title"><i class="pi pi-users"></i>{{ t('leaderboard.topPlayers') }}</div>
                <p-tag [value]="players().length.toString()" severity="secondary" [rounded]="true" />
              </div>
              @if (players().length === 0) {
                <div class="g-empty compact"><span class="g-empty-hint">{{ t('leaderboard.noPlayers') }}</span></div>
              } @else {
                <p-table [value]="players()" size="small" styleClass="lb-table" [tableStyle]="{ 'table-layout': 'fixed', width: '100%' }">
                  <ng-template #header>
                    <tr>
                      <th class="col-rank">{{ t('leaderboard.columns.rank') }}</th>
                      <th>{{ t('leaderboard.columns.player') }}</th>
                      <th class="num">{{ t('leaderboard.columns.rating') }}</th>
                      <th class="num hide-xs">{{ t('leaderboard.columns.winRate') }}</th>
                    </tr>
                  </ng-template>
                  <ng-template #body let-p>
                    <tr class="clickable" [class.top3]="p.rank <= 3" (click)="openProfile(p.playerId)">
                      <td class="col-rank"><span class="rank" [class]="rankClass(p.rank)">{{ p.rank }}</span></td>
                      <td>
                        <div class="who">
                          @if (p.avatarUrl) {
                            <p-avatar [image]="p.avatarUrl" shape="circle" />
                          } @else {
                            <p-avatar [label]="p.displayName.charAt(0).toUpperCase()" shape="circle" />
                          }
                          <span class="who-name">{{ p.displayName }}</span>
                        </div>
                      </td>
                      <td class="num strong">{{ p.rating }}</td>
                      <td class="num hide-xs">{{ p.winRate }}%</td>
                    </tr>
                  </ng-template>
                </p-table>
              }
            </section>
          </div>

          <!-- Achievers -->
          <div class="col-span-12 xl:col-span-4">
            <section class="g-card board">
              <div class="g-card-header">
                <div class="g-card-title gold"><i class="pi pi-star-fill"></i>{{ t('leaderboard.topAchievers') }}</div>
                <p-tag [value]="achievers().length.toString()" severity="secondary" [rounded]="true" />
              </div>
              @if (achievers().length === 0) {
                <div class="g-empty compact"><span class="g-empty-hint">{{ t('leaderboard.noAchievers') }}</span></div>
              } @else {
                <p-table [value]="achievers()" size="small" styleClass="lb-table" [tableStyle]="{ 'table-layout': 'fixed', width: '100%' }">
                  <ng-template #header>
                    <tr>
                      <th class="col-rank">{{ t('leaderboard.columns.rank') }}</th>
                      <th>{{ t('leaderboard.columns.player') }}</th>
                      <th class="num">{{ t('leaderboard.columns.points') }}</th>
                      <th class="num hide-xs">{{ t('leaderboard.columns.count') }}</th>
                    </tr>
                  </ng-template>
                  <ng-template #body let-a>
                    <tr class="clickable" [class.top3]="a.rank <= 3" (click)="openProfile(a.playerId)">
                      <td class="col-rank"><span class="rank" [class]="rankClass(a.rank)">{{ a.rank }}</span></td>
                      <td>
                        <div class="who">
                          @if (a.avatarUrl) {
                            <p-avatar [image]="a.avatarUrl" shape="circle" />
                          } @else {
                            <p-avatar [label]="a.displayName.charAt(0).toUpperCase()" shape="circle" />
                          }
                          <span class="who-name">{{ a.displayName }}</span>
                        </div>
                      </td>
                      <td class="num strong gold">{{ a.achievementPoints }}</td>
                      <td class="num hide-xs">{{ a.achievementCount }}</td>
                    </tr>
                  </ng-template>
                </p-table>
              }
            </section>
          </div>

          <!-- Bots -->
          <div class="col-span-12 xl:col-span-4">
            <section class="g-card board">
              <div class="g-card-header">
                <div class="g-card-title"><i class="pi pi-microchip"></i>{{ t('leaderboard.bots') }}</div>
                <p-tag [value]="bots().length.toString()" severity="secondary" [rounded]="true" />
              </div>
              @if (bots().length === 0) {
                <div class="g-empty compact"><span class="g-empty-hint">{{ t('leaderboard.noBots') }}</span></div>
              } @else {
                <p-table [value]="bots()" size="small" styleClass="lb-table" [tableStyle]="{ 'table-layout': 'fixed', width: '100%' }">
                  <ng-template #header>
                    <tr>
                      <th class="col-rank">{{ t('leaderboard.columns.rank') }}</th>
                      <th>{{ t('leaderboard.columns.bot') }}</th>
                      <th class="num">{{ t('leaderboard.columns.rating') }}</th>
                      <th class="hide-xs author">{{ t('leaderboard.columns.author') }}</th>
                    </tr>
                  </ng-template>
                  <ng-template #body let-b>
                    <tr class="clickable" [class.top3]="b.rank <= 3" (click)="openProfile(b.playerId)">
                      <td class="col-rank"><span class="rank" [class]="rankClass(b.rank)">{{ b.rank }}</span></td>
                      <td>
                        <div class="who">
                          <p-avatar icon="pi pi-microchip" shape="circle" styleClass="bot-avatar" />
                          <span class="who-name">{{ b.displayName }}</span>
                        </div>
                      </td>
                      <td class="num strong">{{ b.rating }}</td>
                      <td class="hide-xs muted-cell">{{ b.author || t('leaderboard.builtIn') }}</td>
                    </tr>
                  </ng-template>
                </p-table>
              }
            </section>
          </div>
        </div>
      }
    </div>

    @if (profileData()) {
      <app-player-profile-popup
        [profile]="profileData()!"
        (closed)="closeProfile()"
      />
    }
  `,
  styles: [`
    .tile { display:flex; align-items:flex-start; justify-content:space-between; gap:1rem; height:100%; padding:1.25rem 1.5rem; }
    .tile-text { display:flex; flex-direction:column; gap:0.25rem; min-width:0; }
    .tile-label { font-size:0.8125rem; font-weight:600; color:var(--text-color-secondary); }
    .tile-value { font-size:2rem; font-weight:700; line-height:1.1; font-variant-numeric:tabular-nums; }
    .tile-value.muted { color:var(--text-color-secondary); }
    .tile-sub { font-size:0.8125rem; color:var(--text-color-secondary); }
    .tile-icon { font-size:1.5rem; color:var(--text-color-secondary); opacity:0.7; }
    .tile-me { background:linear-gradient(135deg, color-mix(in srgb, var(--p-yellow-400) 16%, var(--surface-card)), var(--surface-card)); border-color:color-mix(in srgb, var(--p-yellow-400) 35%, transparent); }
    .tile-me .tile-value { color:var(--p-yellow-400); }
    .tile-me .tile-icon { color:var(--p-yellow-400); opacity:0.9; }

    .board { display:flex; flex-direction:column; padding-bottom:0.75rem; }
    .board .g-card-header { padding-bottom:0.5rem; }
    .g-card-title i { color:var(--text-color-secondary); font-size:0.9375rem; }
    .g-card-title.gold, .g-card-title.gold i { color:var(--p-yellow-400); }
    .g-empty.compact { padding:1.5rem 0.5rem; }

    :host ::ng-deep .lb-table .p-datatable-thead > tr > th { background:transparent; font-size:0.6875rem; font-weight:600; text-transform:uppercase; letter-spacing:0.06em; color:var(--text-color-secondary); padding:0.5rem 0.5rem; }
    :host ::ng-deep .lb-table .p-datatable-tbody > tr { background:transparent; }
    :host ::ng-deep .lb-table .p-datatable-tbody > tr > td { padding:0.5rem 0.5rem; border-color:color-mix(in srgb, var(--surface-border) 60%, transparent); }
    :host ::ng-deep .lb-table .p-datatable-tbody > tr.clickable { cursor:pointer; transition:background-color var(--transition-duration); }
    :host ::ng-deep .lb-table .p-datatable-tbody > tr.clickable:hover { background:var(--surface-hover); }
    :host ::ng-deep .lb-table .p-datatable-tbody > tr:last-child > td { border-bottom:none; }
    :host ::ng-deep .lb-table .p-avatar { width:1.75rem; height:1.75rem; font-size:0.75rem; font-weight:700; flex-shrink:0; }
    :host ::ng-deep .lb-table .bot-avatar { background:color-mix(in srgb, var(--p-yellow-400) 18%, transparent); color:var(--p-yellow-400); }

    .col-rank { width:2.75rem; }
    th.num { width:4.5rem; }
    th.hide-xs { width:4.5rem; }
    th.author { width:5.5rem; }
    .num { text-align:right; font-variant-numeric:tabular-nums; white-space:nowrap; }
    .strong { font-weight:700; }
    .gold { color:var(--p-yellow-400); }
    .muted-cell { color:var(--text-color-secondary); font-size:0.8125rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .who { display:flex; align-items:center; gap:0.5rem; min-width:0; }
    .who-name { font-weight:500; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .rank { display:inline-flex; align-items:center; justify-content:center; min-width:1.5rem; height:1.5rem; padding:0 0.25rem; border-radius:50%; font-size:0.75rem; font-weight:700; color:var(--text-color-secondary); font-variant-numeric:tabular-nums; }
    .rank.gold { background:color-mix(in srgb, var(--p-yellow-400) 22%, transparent); color:var(--p-yellow-400); }
    .rank.silver { background:color-mix(in srgb, var(--p-surface-300) 22%, transparent); color:var(--p-surface-200); }
    .rank.bronze { background:color-mix(in srgb, var(--p-orange-400) 22%, transparent); color:var(--p-orange-400); }
    @media (max-width:479px) { .hide-xs { display:none; } }
  `],
})
export class LeaderboardComponent implements OnInit {

  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  readonly players = signal<LeaderboardPlayerEntry[]>([]);
  readonly achievers = signal<LeaderboardAchieverEntry[]>([]);
  readonly bots = signal<LeaderboardBotEntry[]>([]);
  readonly playerCount = signal<number>(0);
  readonly botCount = signal<number>(0);
  readonly loading = signal<boolean>(true);
  readonly profileData = signal<PlayerProfileResponse | null>(null);

  readonly currentUserEntry = computed(() => {
    const name = this.auth.user()?.displayName;
    if (!name) return null;
    return this.players().find((p) => p.displayName === name) ?? null;
  });

  ngOnInit(): void {
    this.api.getLeaderboard().subscribe({
      next: (res) => {
        this.players.set(res.players.slice(0, 15));
        this.achievers.set(res.topAchievers);
        this.bots.set(res.bots);
        this.playerCount.set(res.playerCount);
        this.botCount.set(res.botCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  rankClass(rank: number): string {
    return rank === 1 ? 'gold' : rank === 2 ? 'silver' : rank === 3 ? 'bronze' : '';
  }

  openProfile(playerId: string): void {
    this.api.getLeaderboardProfile(playerId).subscribe({
      next: (profile) => this.profileData.set(profile),
    });
  }

  closeProfile(): void {
    this.profileData.set(null);
  }

}
