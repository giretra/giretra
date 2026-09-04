import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { ApiService, BlockedUserResponse } from '../../../core/services/api.service';

@Component({
  selector: 'app-blocked-section',
  standalone: true,
  imports: [FormsModule, TranslocoDirective, ButtonModule, AvatarModule, TagModule, InputTextModule],
  template: `
    <ng-container *transloco="let t">
      <div class="g-panel-head">
        <div class="g-panel-text">
          <span class="g-panel-title">{{ t('settings.tabs.blocked') }}</span>
          <span class="g-form-desc">{{ t('settings.blocked.hint') }}</span>
        </div>
      </div>
      <div class="g-divider mx"></div>
      <div class="g-panel-body">
      <div class="block-form">
        <input
          pInputText
          fluid
          type="text"
          name="blockUsername"
          [placeholder]="t('settings.blocked.searchPlaceholder')"
          [(ngModel)]="blockUsername"
          (keydown.enter)="blockUser()"
        />
        <p-button icon="pi pi-ban" [label]="t('settings.blocked.block')" severity="danger" [disabled]="!blockUsername.trim()" (onClick)="blockUser()" />
      </div>

      <div class="g-list-head">
        {{ t('settings.blocked.blockedUsers') }}
        <p-tag [value]="blockedUsers().length.toString()" severity="secondary" [rounded]="true" />
      </div>
      @if (blockedUsers().length === 0) {
        <div class="g-empty compact">
          <span class="g-empty-icon"><i class="pi pi-shield"></i></span>
          <span class="g-empty-hint">{{ t('settings.blocked.noBlocked') }}</span>
        </div>
      } @else {
        <ul class="g-user-list">
          @for (user of blockedUsers(); track user.blockId) {
            <li class="g-user-row">
              <p-avatar [label]="user.displayName.charAt(0).toUpperCase()" shape="circle" />
              <div class="g-user-info">
                <span class="g-user-name">{{ user.displayName }}</span>
                <span class="g-user-sub">&#64;{{ user.username }}</span>
              </div>
              <p-button [label]="t('settings.blocked.unblock')" severity="secondary" [outlined]="true" size="small" (onClick)="unblockUser(user.blockId)" />
            </li>
          }
        </ul>
      }
      </div>
    </ng-container>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; }
    .g-panel-text { display:flex; flex-direction:column; gap:0.125rem; }
    .g-divider.mx { margin:0 1.5rem; }
    @media (min-width:1200px) { .g-divider.mx { margin:0 2rem; } }
    .block-form { display:flex; gap:0.5rem; max-width:32rem; }
    .g-panel-body { gap:0.5rem; }
    .g-list-head { margin:1rem 0 0.25rem; }
    .g-panel-body > .g-list-head:first-child { margin-top:0; }
    .g-empty.compact { padding:1.5rem 0.5rem; }
  `],
})
export class BlockedSectionComponent implements OnInit {

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
