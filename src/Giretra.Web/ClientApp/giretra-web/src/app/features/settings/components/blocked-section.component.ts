import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Ban, Shield } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
import { ApiService, BlockedUserResponse } from '../../../core/services/api.service';

@Component({
  selector: 'app-blocked-section',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, TranslocoDirective],
  template: `
    <div class="blocked" *transloco="let t">
      <!-- Block input -->
      <div class="block-form">
        <input
          type="text"
          class="block-input"
          [placeholder]="t('settings.blocked.searchPlaceholder')"
          [(ngModel)]="blockUsername"
          (keydown.enter)="blockUser()"
        />
        <button
          class="btn btn-destructive"
          (click)="blockUser()"
          [disabled]="!blockUsername.trim()"
        >
          <i-lucide [img]="BanIcon" [size]="14"></i-lucide>
          {{ t('settings.blocked.block') }}
        </button>
      </div>

      <!-- Blocked list -->
      <div class="section">
        <h3 class="section-title">
          <i-lucide [img]="ShieldIcon" [size]="14"></i-lucide>
          {{ t('settings.blocked.blockedUsers') }}
        </h3>
        @if (blockedUsers().length === 0) {
          <div class="empty-state">{{ t('settings.blocked.noBlocked') }}</div>
        } @else {
          @for (user of blockedUsers(); track user.blockId) {
            <div class="user-row">
              <div class="user-avatar-sm">{{ user.displayName.charAt(0).toUpperCase() }}</div>
              <div class="user-info">
                <span class="user-display">{{ user.displayName }}</span>
                <span class="user-username">&#64;{{ user.username }}</span>
              </div>
              <button class="btn btn-sm" (click)="unblockUser(user.blockId)">
                {{ t('settings.blocked.unblock') }}
              </button>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .blocked { display:flex; flex-direction:column; gap:1.25rem; }
    .block-form { display:flex; gap:0.5rem; }
    .block-input { flex:1; padding:0.5rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--input)); color:hsl(var(--foreground)); font-size:0.875rem; outline:none; transition:all 0.15s ease; }
    .block-input:focus { border-color:hsl(var(--ring)); box-shadow:0 0 0 2px hsl(var(--ring)/0.2); }
    .section { display:flex; flex-direction:column; gap:0.5rem; }
    .section-title { display:flex; align-items:center; gap:0.375rem; font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.06em; margin:0; }
    .user-row { display:flex; align-items:center; gap:0.625rem; padding:0.625rem 0.75rem; border-bottom:1px solid hsl(var(--border)/0.5); transition:all 0.15s ease; }
    .user-row:last-child { border-bottom:none; }
    .user-row:hover { background:hsl(var(--foreground)/0.03); }
    .user-avatar-sm { width:2rem; height:2rem; border-radius:50%; background:hsl(var(--destructive)/0.15); border:2px solid hsl(var(--destructive)/0.3); display:flex; align-items:center; justify-content:center; font-size:0.75rem; font-weight:700; color:hsl(var(--destructive)); flex-shrink:0; }
    .user-info { display:flex; flex-direction:column; gap:0.0625rem; flex:1; min-width:0; }
    .user-display { font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .user-username { font-size:0.6875rem; color:hsl(var(--muted-foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .btn { display:inline-flex; align-items:center; gap:0.375rem; padding:0.375rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--secondary)); color:hsl(var(--foreground)); font-size:0.75rem; font-weight:500; cursor:pointer; transition:all 0.15s ease; white-space:nowrap; }
    .btn:hover { background:hsl(var(--muted)); }
    .btn:disabled { opacity:0.5; cursor:not-allowed; }
    .btn-sm { padding:0.25rem 0.625rem; font-size:0.6875rem; }
    .btn-destructive { background:hsl(var(--destructive)); color:hsl(var(--destructive-foreground)); border-color:hsl(var(--destructive)); }
    .btn-destructive:hover { opacity:0.9; }
    .btn-destructive:disabled { opacity:0.5; }
    .empty-state { padding:2rem; text-align:center; color:hsl(var(--muted-foreground)); font-size:0.8125rem; }
  `],
})
export class BlockedSectionComponent implements OnInit {
  readonly BanIcon = Ban;
  readonly ShieldIcon = Shield;

  private readonly api = inject(ApiService);

  readonly blockedUsers = signal<BlockedUserResponse[]>([]);
  blockUsername = '';

  ngOnInit(): void {
    this.loadBlocked();
  }

  private loadBlocked(): void {
    this.api.getBlockedUsers().subscribe({
      next: (users) => this.blockedUsers.set(users),
    });
  }

  blockUser(): void {
    const username = this.blockUsername.trim();
    if (!username) return;
    this.api.blockUser(username).subscribe({
      next: () => {
        this.blockUsername = '';
        this.loadBlocked();
      },
    });
  }

  unblockUser(blockId: string): void {
    this.api.unblockUser(blockId).subscribe({
      next: () => this.loadBlocked(),
    });
  }
}
