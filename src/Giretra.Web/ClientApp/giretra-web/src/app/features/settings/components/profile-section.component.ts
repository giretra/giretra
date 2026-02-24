import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Pencil, Upload, Trash2, EyeOff, Eye } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ApiService, ProfileResponse } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-profile-section',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      @if (profile(); as p) {
        <div class="profile">
          <!-- Avatar -->
          <div class="avatar-section">
            <div class="avatar-preview">
              @if (p.avatarUrl) {
                <img [src]="p.avatarUrl" [alt]="t('settings.profile.avatar')" class="avatar-img" />
              } @else {
                <span class="avatar-initial">{{ p.displayName.charAt(0).toUpperCase() }}</span>
              }
            </div>
            <div class="avatar-actions">
              <label class="btn btn-sm">
                <i-lucide [img]="UploadIcon" [size]="14"></i-lucide>
                {{ t('settings.profile.upload') }}
                <input type="file" accept="image/*" (change)="onAvatarSelected($event)" hidden />
              </label>
              @if (p.avatarUrl) {
                <button class="btn btn-sm btn-destructive" (click)="deleteAvatar()">
                  <i-lucide [img]="Trash2Icon" [size]="14"></i-lucide>
                  {{ t('settings.profile.remove') }}
                </button>
              }
            </div>
          </div>

          <!-- Display name -->
          <div class="field">
            <label class="field-label">{{ t('settings.profile.displayName') }}</label>
            @if (editingName()) {
              <div class="name-edit">
                <input
                  type="text"
                  class="name-input"
                  [(ngModel)]="nameValue"
                  (keydown.enter)="saveName()"
                  (keydown.escape)="cancelNameEdit()"
                  maxlength="100"
                />
                <div class="name-edit-actions">
                  @if (nameError()) {
                    <span class="field-error">{{ nameError() }}</span>
                  }
                  <button class="btn btn-sm btn-primary" (click)="saveName()" [disabled]="!!nameError() || savingName()">{{ t('common.save') }}</button>
                  <button class="btn btn-sm" (click)="cancelNameEdit()">{{ t('common.cancel') }}</button>
                </div>
              </div>
            } @else {
              <div class="name-display">
                <span class="name-value">{{ p.displayName }}</span>
                <button class="btn-icon" (click)="startNameEdit()" [title]="t('settings.profile.editDisplayName')">
                  <i-lucide [img]="PencilIcon" [size]="14"></i-lucide>
                </button>
              </div>
            }
            <span class="field-hint">&#64;{{ p.username }}</span>
          </div>

          <!-- ELO visibility -->
          <div class="field">
            <label class="field-label">{{ t('settings.profile.eloVisibility') }}</label>
            <div class="toggle-row">
              <button
                class="toggle"
                [class.toggle-on]="eloPublic()"
                (click)="toggleEloVisibility()"
                [attr.aria-pressed]="eloPublic()"
              >
                <span class="toggle-knob"></span>
              </button>
              <span class="toggle-label">
                @if (eloPublic()) {
                  <i-lucide [img]="EyeIcon" [size]="14"></i-lucide>
                  {{ t('settings.profile.eloPublic') }}
                } @else {
                  <i-lucide [img]="EyeOffIcon" [size]="14"></i-lucide>
                  {{ t('settings.profile.eloHidden') }}
                }
              </span>
            </div>
          </div>

          <!-- Stats grid -->
          <div class="stats-grid">
            <div class="stat-card">
              <span class="stat-value">{{ p.eloRating }}</span>
              <span class="stat-label">{{ t('settings.profile.eloRating') }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-value">{{ p.gamesPlayed }}</span>
              <span class="stat-label">{{ t('settings.profile.gamesPlayed') }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-value">{{ winRate() }}%</span>
              <span class="stat-label">{{ t('settings.profile.winRate') }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-value">{{ p.winStreak }}</span>
              <span class="stat-label">{{ t('settings.profile.winStreak') }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-value">{{ p.bestWinStreak }}</span>
              <span class="stat-label">{{ t('settings.profile.bestStreak') }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-value">{{ memberSince() }}</span>
              <span class="stat-label">{{ t('settings.profile.memberSince') }}</span>
            </div>
          </div>
        </div>
      } @else {
        <div class="loading">{{ t('settings.profile.loadingProfile') }}</div>
      }
    </ng-container>
  `,
  styles: [`
    .profile { display:flex; flex-direction:column; gap:1.5rem; }
    .avatar-section { display:flex; align-items:center; gap:1rem; }
    .avatar-preview { width:4.5rem; height:4.5rem; border-radius:50%; background:hsl(var(--primary)/0.2); border:3px solid hsl(var(--primary)); display:flex; align-items:center; justify-content:center; overflow:hidden; flex-shrink:0; }
    .avatar-img { width:100%; height:100%; object-fit:cover; }
    .avatar-initial { font-size:1.75rem; font-weight:700; color:hsl(var(--primary)); text-transform:uppercase; }
    .avatar-actions { display:flex; gap:0.5rem; flex-wrap:wrap; }
    .btn { display:inline-flex; align-items:center; gap:0.375rem; padding:0.375rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--secondary)); color:hsl(var(--foreground)); font-size:0.75rem; font-weight:500; cursor:pointer; transition:all 0.15s ease; }
    .btn:hover { background:hsl(var(--muted)); }
    .btn-sm { padding:0.25rem 0.625rem; font-size:0.6875rem; }
    .btn-primary { background:hsl(var(--primary)); color:hsl(var(--primary-foreground)); border-color:hsl(var(--primary)); }
    .btn-primary:hover { opacity:0.9; }
    .btn-primary:disabled { opacity:0.5; cursor:not-allowed; }
    .btn-destructive { background:transparent; color:hsl(var(--destructive)); border-color:hsl(var(--destructive)/0.3); }
    .btn-destructive:hover { background:hsl(var(--destructive)/0.1); }
    .field { display:flex; flex-direction:column; gap:0.375rem; }
    .field-label { font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.06em; }
    .field-hint { font-size:0.75rem; color:hsl(var(--muted-foreground)); }
    .field-error { font-size:0.6875rem; color:hsl(var(--destructive)); }
    .name-display { display:flex; align-items:center; gap:0.5rem; }
    .name-value { font-size:1rem; font-weight:600; color:hsl(var(--foreground)); }
    .btn-icon { display:flex; align-items:center; justify-content:center; width:1.75rem; height:1.75rem; border-radius:var(--radius); border:none; background:transparent; color:hsl(var(--muted-foreground)); cursor:pointer; transition:all 0.15s ease; }
    .btn-icon:hover { color:hsl(var(--foreground)); background:hsl(var(--foreground)/0.08); }
    .name-edit { display:flex; flex-direction:column; gap:0.375rem; }
    .name-input { padding:0.5rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--input)); color:hsl(var(--foreground)); font-size:0.875rem; outline:none; transition:all 0.15s ease; }
    .name-input:focus { border-color:hsl(var(--ring)); box-shadow:0 0 0 2px hsl(var(--ring)/0.2); }
    .name-edit-actions { display:flex; align-items:center; gap:0.5rem; }
    .toggle-row { display:flex; align-items:center; gap:0.625rem; }
    .toggle { position:relative; width:2.5rem; height:1.375rem; border-radius:9999px; border:none; background:hsl(var(--muted)); cursor:pointer; transition:all 0.15s ease; padding:0; }
    .toggle-on { background:hsl(var(--primary)); }
    .toggle-knob { position:absolute; top:0.1875rem; left:0.1875rem; width:1rem; height:1rem; border-radius:50%; background:hsl(var(--foreground)); transition:all 0.15s ease; }
    .toggle-on .toggle-knob { left:calc(100% - 1.1875rem); }
    .toggle-label { display:flex; align-items:center; gap:0.375rem; font-size:0.8125rem; color:hsl(var(--muted-foreground)); }
    .stats-grid { display:grid; grid-template-columns:repeat(3, 1fr); gap:0.75rem; }
    .stat-card { display:flex; flex-direction:column; align-items:center; gap:0.25rem; padding:1rem 0.5rem; background:hsl(var(--secondary)); border:1px solid hsl(var(--border)); border-radius:var(--radius); }
    .stat-value { font-size:1.25rem; font-weight:700; color:hsl(var(--foreground)); }
    .stat-label { font-size:0.625rem; font-weight:500; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.04em; }
    .loading { padding:2rem; text-align:center; color:hsl(var(--muted-foreground)); font-size:0.875rem; }
    @media (max-width:480px) {
      .stats-grid { grid-template-columns:repeat(2, 1fr); }
    }
  `],
})
export class ProfileSectionComponent implements OnInit {
  readonly PencilIcon = Pencil;
  readonly UploadIcon = Upload;
  readonly Trash2Icon = Trash2;
  readonly EyeOffIcon = EyeOff;
  readonly EyeIcon = Eye;

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);

  readonly profile = signal<ProfileResponse | null>(null);
  readonly editingName = signal(false);
  readonly savingName = signal(false);
  readonly nameError = signal<string | null>(null);
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
      },
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

  startNameEdit(): void {
    this.nameValue = this.profile()?.displayName ?? '';
    this.nameError.set(null);
    this.editingName.set(true);
  }

  cancelNameEdit(): void {
    this.editingName.set(false);
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
        this.editingName.set(false);
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
