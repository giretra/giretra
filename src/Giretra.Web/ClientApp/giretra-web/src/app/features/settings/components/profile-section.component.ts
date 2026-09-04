import { Component, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { SkeletonModule } from 'primeng/skeleton';
import { ApiService, ProfileResponse } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { SoundService } from '../../../core/services/sound.service';

@Component({
  selector: 'app-profile-section',
  standalone: true,
  imports: [FormsModule, TranslocoDirective, ButtonModule, AvatarModule, InputTextModule, ToggleSwitchModule, SkeletonModule],
  template: `
    <ng-container *transloco="let t">
      <div class="g-panel-head">
        <span class="g-panel-title">{{ t('settings.tabs.profile') }}</span>
      </div>
      <div class="g-divider mx"></div>

      @if (profile(); as p) {
        <div class="g-panel-body">
          <!-- Photo -->
          <div class="g-form-row">
            <div class="g-form-text">
              <span class="g-form-label">{{ t('settings.profile.photo') }}</span>
              <span class="g-form-desc">{{ t('settings.profile.photoHint') }}</span>
            </div>
            <div class="g-form-control photo">
              @if (p.avatarUrl) {
                <p-avatar [image]="p.avatarUrl" shape="circle" size="large" styleClass="photo-avatar" />
              } @else {
                <p-avatar [label]="p.displayName.charAt(0).toUpperCase()" shape="circle" size="large" styleClass="photo-avatar" />
              }
              <input #fileInput type="file" accept="image/*" hidden (change)="onAvatarSelected($event)" />
              <div class="photo-actions">
                <p-button icon="pi pi-upload" [label]="t('settings.profile.upload')" severity="secondary" [outlined]="true" size="small" (onClick)="fileInput.click()" />
                @if (p.avatarUrl) {
                  <p-button icon="pi pi-trash" [label]="t('settings.profile.remove')" severity="danger" [text]="true" size="small" (onClick)="deleteAvatar()" />
                }
              </div>
            </div>
          </div>

          <div class="g-divider"></div>

          <!-- Display name -->
          <div class="g-form-row">
            <div class="g-form-text">
              <span class="g-form-label">{{ t('settings.profile.displayName') }}</span>
              <span class="g-form-desc">{{ t('settings.profile.displayNameHint') }}</span>
            </div>
            <div class="g-form-control name">
              <div class="name-line">
                <input
                  pInputText
                  fluid
                  type="text"
                  name="displayName"
                  [ngModel]="nameValue"
                  (ngModelChange)="onNameInput($event)"
                  [invalid]="!!nameError()"
                  (keydown.enter)="saveName()"
                  (keydown.escape)="cancelNameEdit()"
                  maxlength="100"
                />
                @if (nameDirty()) {
                  <p-button icon="pi pi-times" severity="secondary" [text]="true" [attr.aria-label]="t('common.cancel')" (onClick)="cancelNameEdit()" />
                  <p-button icon="pi pi-check" [label]="t('common.save')" [disabled]="!!nameError()" [loading]="savingName()" (onClick)="saveName()" />
                }
              </div>
              @if (nameError()) {
                <small class="field-error">{{ nameError() }}</small>
              }
            </div>
          </div>

          <div class="g-divider"></div>

          <!-- Username / member since -->
          <div class="g-form-row">
            <div class="g-form-text">
              <span class="g-form-label">{{ t('settings.profile.username') }}</span>
              <span class="g-form-desc">{{ t('settings.profile.usernameHint') }}</span>
            </div>
            <div class="g-form-control readonly">
              <span class="readonly-value">&#64;{{ p.username }}</span>
              <span class="readonly-meta"><i class="pi pi-calendar"></i>{{ t('settings.profile.memberSince') }} {{ memberSince() }}</span>
            </div>
          </div>

          <div class="g-divider"></div>

          <!-- Statistics -->
          <div class="g-form-row">
            <div class="g-form-text">
              <span class="g-form-label">{{ t('settings.stats.title') }}</span>
              <span class="g-form-desc">{{ t('settings.stats.hint') }}</span>
            </div>
            <div class="g-form-control stats-grid">
              <div class="stat"><span class="stat-value">{{ p.eloRating }}</span><span class="stat-label">{{ t('settings.profile.eloRating') }}</span></div>
              <div class="stat"><span class="stat-value">{{ p.gamesPlayed }}</span><span class="stat-label">{{ t('settings.profile.gamesPlayed') }}</span></div>
              <div class="stat"><span class="stat-value">{{ winRate() }}%</span><span class="stat-label">{{ t('settings.profile.winRate') }}</span></div>
              <div class="stat"><span class="stat-value">{{ p.winStreak }}</span><span class="stat-label">{{ t('settings.profile.winStreak') }}</span></div>
              <div class="stat"><span class="stat-value">{{ p.bestWinStreak }}</span><span class="stat-label">{{ t('settings.profile.bestStreak') }}</span></div>
              <button type="button" class="stat gold" (click)="goToAchievements()">
                <span class="stat-value"><i class="pi pi-star-fill"></i>{{ achievementCount() }}</span>
                <span class="stat-label">{{ t('achievements.page.title') }} <i class="pi pi-arrow-right"></i></span>
              </button>
            </div>
          </div>

          <div class="g-divider"></div>

          <!-- Preferences -->
          <div class="g-form-row">
            <div class="g-form-text">
              <span class="g-form-label">{{ t('settings.preferences.title') }}</span>
              <span class="g-form-desc">{{ t('settings.preferences.hint') }}</span>
            </div>
            <div class="g-form-control prefs">
              <label class="pref">
                <span class="pref-text">
                  <span class="pref-label"><i class="pi" [class.pi-volume-up]="!soundService.muted()" [class.pi-volume-off]="soundService.muted()"></i>{{ t('settings.profile.sound') }}</span>
                  <span class="pref-hint">{{ t('settings.profile.soundHint') }}</span>
                </span>
                <p-toggleswitch [ngModel]="!soundService.muted()" (ngModelChange)="soundService.toggleMute()" [ariaLabel]="t('settings.profile.sound')" />
              </label>
              <label class="pref">
                <span class="pref-text">
                  <span class="pref-label"><i class="pi" [class.pi-eye]="eloPublic()" [class.pi-eye-slash]="!eloPublic()"></i>{{ t('settings.profile.publicElo') }}</span>
                  <span class="pref-hint">{{ t('settings.profile.publicEloHint') }}</span>
                </span>
                <p-toggleswitch [ngModel]="eloPublic()" (ngModelChange)="toggleEloVisibility()" [ariaLabel]="t('settings.profile.eloVisibility')" />
              </label>
            </div>
          </div>
        </div>
      } @else {
        <div class="g-panel-body">
          <p-skeleton height="3rem" borderRadius="12px" />
          <p-skeleton height="3rem" borderRadius="12px" />
          <p-skeleton height="8rem" borderRadius="12px" />
        </div>
      }
    </ng-container>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; }
    .g-divider.mx { margin:0 1.5rem; }
    @media (min-width:1200px) { .g-divider.mx { margin:0 2rem; } }

    .photo { display:flex; align-items:center; gap:1rem; flex-wrap:wrap; }
    :host ::ng-deep .photo-avatar { width:3.5rem; height:3.5rem; font-size:1.25rem; font-weight:700; background:color-mix(in srgb, var(--p-primary-color) 22%, transparent); color:var(--p-primary-300); border:2px solid var(--surface-border); }
    .photo-actions { display:flex; align-items:center; gap:0.25rem; }

    .name { display:flex; flex-direction:column; gap:0.375rem; }
    .name-line { display:flex; align-items:center; gap:0.5rem; }
    .field-error { color:var(--p-red-400); font-size:0.8125rem; }

    .readonly { display:flex; flex-direction:column; gap:0.375rem; }
    .readonly-value { font-size:1rem; }
    .readonly-meta { display:inline-flex; align-items:center; gap:0.375rem; font-size:0.8125rem; color:var(--text-color-secondary); }

    .stats-grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(7.5rem, 1fr)); gap:0.625rem; }
    .stat { display:flex; flex-direction:column; gap:0.125rem; padding:0.75rem 0.875rem; border-radius:0.875rem; background:var(--p-surface-800); border:1px solid transparent; color:inherit; text-align:left; }
    .stat-value { font-size:1.375rem; font-weight:700; line-height:1.1; font-variant-numeric:tabular-nums; display:inline-flex; align-items:center; gap:0.375rem; }
    .stat-value i { font-size:0.875rem; }
    .stat-label { font-size:0.75rem; color:var(--text-color-secondary); }
    .stat-label i { font-size:0.625rem; margin-left:0.125rem; }
    .stat.gold { cursor:pointer; transition:border-color var(--transition-duration), background-color var(--transition-duration); }
    .stat.gold .stat-value { color:var(--p-yellow-400); }
    .stat.gold:hover { border-color:color-mix(in srgb, var(--p-yellow-400) 45%, transparent); background:color-mix(in srgb, var(--p-yellow-400) 8%, var(--p-surface-800)); }

    .prefs { display:flex; flex-direction:column; gap:0.5rem; }
    .pref { display:flex; align-items:center; justify-content:space-between; gap:1rem; padding:0.75rem 1rem; border-radius:0.875rem; background:var(--p-surface-800); cursor:pointer; }
    .pref-text { display:flex; flex-direction:column; gap:0.125rem; }
    .pref-label { display:inline-flex; align-items:center; gap:0.5rem; font-weight:500; }
    .pref-label i { color:var(--text-color-secondary); font-size:0.875rem; }
    .pref-hint { font-size:0.8125rem; color:var(--text-color-secondary); }
  `],
})
export class ProfileSectionComponent implements OnInit {

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly soundService = inject(SoundService);
  private readonly transloco = inject(TranslocoService);

  readonly profile = signal<ProfileResponse | null>(null);
  readonly achievementCount = signal(0);
  readonly savingName = signal(false);
  readonly nameError = signal<string | null>(null);
  readonly nameDirty = signal(false);
  readonly eloPublic = signal(false);

  nameValue = '';

  ngOnInit(): void {
    this.loadProfile();
  }

  private loadProfile(): void {
    this.api.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.eloPublic.set(p.eloIsPublic);
        this.nameValue = p.displayName;
        this.nameDirty.set(false);
      },
    });
    this.api.getMyAchievementShowcase().subscribe({
      next: (data) => this.achievementCount.set(data.earnedCount),
    });
  }

  winRate(): number {
    const p = this.profile();
    if (!p || p.gamesPlayed === 0) return 0;
    return Math.round((p.gamesWon / p.gamesPlayed) * 100);
  }

  memberSince(): string {
    const p = this.profile();
    if (!p) return '';
    return new Date(p.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short' });
  }

  onNameInput(value: string): void {
    this.nameValue = value;
    const current = this.profile()?.displayName ?? '';
    this.nameDirty.set(value.trim() !== current);
    this.nameError.set(this.nameDirty() ? this.validateName(value) : null);
  }

  cancelNameEdit(): void {
    this.nameValue = this.profile()?.displayName ?? '';
    this.nameDirty.set(false);
    this.nameError.set(null);
  }

  private validateName(name: string): string | null {
    const trimmed = name.trim();
    if (trimmed.length < 3) return this.transloco.translate('settings.profile.validation.tooShort');
    if (trimmed.length > 100) return this.transloco.translate('settings.profile.validation.tooLong');
    if (!/^[a-zA-Z0-9 \-_.]+$/.test(trimmed)) return this.transloco.translate('settings.profile.validation.invalidChars');
    if (!/[a-zA-Z0-9]/.test(trimmed)) return this.transloco.translate('settings.profile.validation.needAlphanumeric');
    if (/  /.test(trimmed)) return this.transloco.translate('settings.profile.validation.consecutiveSpaces');
    return null;
  }

  saveName(): void {
    if (!this.nameDirty() || this.savingName()) return;
    const trimmed = this.nameValue.trim();
    const error = this.validateName(trimmed);
    if (error) {
      this.nameError.set(error);
      return;
    }
    this.savingName.set(true);
    this.api.updateDisplayName(trimmed).subscribe({
      next: () => {
        const p = this.profile();
        if (p) this.profile.set({ ...p, displayName: trimmed });
        this.auth.updateLocalDisplayName(trimmed);
        this.nameValue = trimmed;
        this.nameDirty.set(false);
        this.savingName.set(false);
      },
      error: () => {
        this.savingName.set(false);
      },
    });
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.api.uploadAvatar(file).subscribe({
      next: (res) => {
        const p = this.profile();
        if (p) this.profile.set({ ...p, avatarUrl: res.avatarUrl });
      },
    });
  }

  deleteAvatar(): void {
    this.api.deleteAvatar().subscribe({
      next: () => {
        const p = this.profile();
        if (p) this.profile.set({ ...p, avatarUrl: null });
      },
    });
  }

  goToAchievements(): void {
    this.router.navigate(['/achievements']);
  }

  toggleEloVisibility(): void {
    const newVal = !this.eloPublic();
    this.api.updateEloVisibility(newVal).subscribe({
      next: () => {
        this.eloPublic.set(newVal);
        const p = this.profile();
        if (p) this.profile.set({ ...p, eloIsPublic: newVal });
      },
    });
  }
}
